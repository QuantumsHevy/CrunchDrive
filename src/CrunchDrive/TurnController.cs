using CrunchDrive.Core;
using UnityEngine;

namespace CrunchDrive
{
    // Turns the craft so its nose points along DesiredNose, in the style of MechJeb's attitude controller:
    // an outer loop picks the fastest turning rate the craft can still brake from (square-root law),
    // an inner loop drives the control inputs to match that rate.
    //
    // It needs no knowledge of the craft's torque. It measures it: SAS is switched off and four short
    // test pulses (baseline drift, pitch, yaw, roll) show which way each input turns the craft and how
    // hard. If pitch or yaw does nothing, the controller reports Failed and the caller falls back to SAS.
    //
    // It steers through Vessel.OnFlyByWire, so it works only while the vessel is unpacked (physics, not on-rails warp).
    public class TurnController
    {
        public bool Running { get; private set; }
        public bool Failed { get; private set; }
        public bool Calibrated { get { return stage == Stage.Steer; } }
        public Vector3d DesiredNose = new Vector3d(0.0, 1.0, 0.0);   // World-space direction the nose should point. Updated by the caller.

        enum Stage { Baseline, Pitch, Yaw, Roll, Steer }

        Vessel vessel;
        Stage stage;
        double stageTime;
        bool stageStarted;
        Vector3d omegaAtStageStart;
        Vector3d drift;             // Angular velocity change per second with no input.
        Vector3d axisRight, axisBelly, axisUp;   // Craft axes fixed at the start, used for calibration.

        // Measured angular acceleration per full input, signed (rad/s^2 about the craft's right, belly and up axes).
        double alphaPitch, alphaYaw, alphaRoll;

        float outPitch, outYaw, outRoll;
        float lastStamp = -1f;      // Time.fixedTime of the last update, so a second callback in the same tick reuses its output.

        public void Begin(Vessel v)
        {
            End();
            vessel = v;
            Failed = false;
            stage = Stage.Baseline;
            stageTime = 0.0;
            stageStarted = false;
            alphaPitch = alphaYaw = alphaRoll = 0.0;
            outPitch = outYaw = outRoll = 0f;
            lastStamp = -1f;

            Transform rt = v.ReferenceTransform;
            axisRight = (Vector3d)rt.right;
            axisBelly = (Vector3d)rt.forward;
            axisUp = (Vector3d)rt.up;

            v.OnFlyByWire += Fly;
            Running = true;
            Debug.Log("[CrunchDrive] Attitude controller started; calibrating.");
        }

        public void End()
        {
            if (vessel != null) vessel.OnFlyByWire -= Fly;
            Running = false;
            vessel = null;
        }

        static bool Sane(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }

        void Fly(FlightCtrlState st)
        {
            if (!Running || vessel == null || vessel.rootPart == null || vessel.rootPart.rb == null) return;

            if (Time.fixedTime == lastStamp) { Apply(st); return; }   // Same tick, same answer.
            lastStamp = Time.fixedTime;

            double dt = TimeWarp.fixedDeltaTime;
            Vector3d omega = (Vector3d)vessel.rootPart.rb.angularVelocity;   // World frame, rad/s.
            if (!Sane(omega.x) || !Sane(omega.y) || !Sane(omega.z)) { outPitch = outYaw = outRoll = 0f; Apply(st); return; }

            if (stage != Stage.Steer) Calibrate(omega, dt);
            if (stage == Stage.Steer && Running) Steer(omega);

            Apply(st);
        }

        void Apply(FlightCtrlState st)
        {
            st.pitch = outPitch;
            st.yaw = outYaw;
            st.roll = outRoll;
        }

        // ---- Calibration: baseline, then one test pulse per axis ----

        void Calibrate(Vector3d omega, double dt)
        {
            double T = CrunchTuning.TurnProbeSeconds;
            float pulse = (float)CrunchTuning.TurnProbeInput;

            if (!stageStarted)
            {
                stageStarted = true;
                stageTime = 0.0;
                omegaAtStageStart = omega;
            }

            outPitch = outYaw = outRoll = 0f;
            if (stage == Stage.Pitch) outPitch = pulse;
            else if (stage == Stage.Yaw) outYaw = pulse;
            else if (stage == Stage.Roll) outRoll = pulse;

            stageTime += dt;
            if (stageTime < T) return;

            // The pulse is over: what changed, beyond the drift seen with no input?
            Vector3d change = omega - omegaAtStageStart;
            double span = stageTime;
            outPitch = outYaw = outRoll = 0f;

            switch (stage)
            {
                case Stage.Baseline:
                    drift = change * (1.0 / span);
                    stage = Stage.Pitch;
                    break;
                case Stage.Pitch:
                    alphaPitch = Vector3d.Dot(change - drift * span, axisRight) / (pulse * span);
                    stage = Stage.Yaw;
                    break;
                case Stage.Yaw:
                    alphaYaw = Vector3d.Dot(change - drift * span, axisBelly) / (pulse * span);
                    stage = Stage.Roll;
                    break;
                case Stage.Roll:
                    alphaRoll = Vector3d.Dot(change - drift * span, axisUp) / (pulse * span);
                    Finish();
                    break;
            }
            stageStarted = false;
        }

        void Finish()
        {
            bool ok = Sane(alphaPitch) && Sane(alphaYaw)
                      && System.Math.Abs(alphaPitch) >= CrunchTuning.TurnMinAuthority
                      && System.Math.Abs(alphaYaw) >= CrunchTuning.TurnMinAuthority;

            Debug.Log(string.Format("[CrunchDrive] Attitude controller calibration: pitch {0:E2}, yaw {1:E2}, roll {2:E2} rad/s^2 per full input{3}.",
                alphaPitch, alphaYaw, alphaRoll, ok ? "" : " (not enough torque)"));

            if (!ok)
            {
                Failed = true;
                End();
                return;
            }
            stage = Stage.Steer;
        }

        // ---- Steering ----

        void Steer(Vector3d omega)
        {
            Transform rt = vessel.ReferenceTransform;
            Vector3d nose = ((Vector3d)rt.up).normalized;
            Vector3d right = (Vector3d)rt.right;
            Vector3d belly = (Vector3d)rt.forward;

            Vector3d want = DesiredNose.normalized;
            double cosA = System.Math.Max(-1.0, System.Math.Min(1.0, Vector3d.Dot(nose, want)));
            double theta = System.Math.Acos(cosA);

            // Axis that swings the nose onto the target. Dead ahead needs nothing; dead behind can use any axis.
            Vector3d axis = Vector3d.Cross(nose, want);
            double am = axis.magnitude;
            if (am > 1e-9) axis = axis * (1.0 / am);
            else axis = theta > 1.0 ? right : new Vector3d(0.0, 0.0, 0.0);

            // Angular acceleration available about that axis (it lies in the pitch-yaw plane).
            double eP = Vector3d.Dot(axis, right), eY = Vector3d.Dot(axis, belly);
            double aP = System.Math.Max(System.Math.Abs(alphaPitch), 1e-9);
            double aY = System.Math.Max(System.Math.Abs(alphaYaw), 1e-9);
            double inv = System.Math.Sqrt((eP / aP) * (eP / aP) + (eY / aY) * (eY / aY));
            double alpha = inv > 1e-12 ? 1.0 / inv : System.Math.Min(aP, aY);

            double maxRate = CrunchTuning.TurnMaxRateDegPerSec * System.Math.PI / 180.0;
            double rate = CrunchMath.TurnRateLimit(alpha, theta, CrunchTuning.TurnBrakingMargin, maxRate, CrunchTuning.TurnRateGain);
            Vector3d omegaWanted = axis * rate;
            Vector3d rateError = omegaWanted - omega;

            double T = CrunchTuning.TurnResponseSeconds;
            outPitch = Clamp(Vector3d.Dot(rateError, right) / (alphaPitch * T));
            outYaw = Clamp(Vector3d.Dot(rateError, belly) / (alphaYaw * T));

            // Roll is only damped, never steered; a craft without roll torque simply skips it.
            if (System.Math.Abs(alphaRoll) >= CrunchTuning.TurnMinAuthority)
                outRoll = Clamp(-Vector3d.Dot(omega, (Vector3d)rt.up) / (alphaRoll * T));
            else
                outRoll = 0f;
        }

        static float Clamp(double u)
        {
            if (!Sane(u)) return 0f;
            return (float)System.Math.Max(-1.0, System.Math.Min(1.0, u));
        }
    }
}