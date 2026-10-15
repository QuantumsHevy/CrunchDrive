using CrunchDrive.Core;
using UnityEngine;

namespace CrunchDrive
{
    public enum CrunchPhase { Idle, Spooling, Ready, Engaging, Crunching }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class CrunchController : MonoBehaviour
    {
        public static string Status = CrunchText.StatusIdle;
        static CrunchController instance;

        public static CrunchPhase CurrentPhase { get { return instance != null ? instance.phase : CrunchPhase.Idle; } }
        public static void PrimaryAction() { if (instance != null) instance.DoPrimary(); }
        public static void Cancel() { if (instance != null) instance.DoCancel(); }

        CrunchPhase phase = CrunchPhase.Idle;
        bool crunchBackwards;
        bool engageRequested;
        Vessel crunchVessel;
        Vector3d latchedHeading;    // Fixed for the whole crunch unless a target lock re-aims it.
        string lastBodyName;

        ITargetable lockedTarget;   // Target the crunch is homing on, if any.
        bool targetLocked;
        string lockedTargetName = "";

        double spoolElapsed;        // Seconds spent spooling.
        double engageElapsed;       // Seconds since engine thrust was detected.
        double elapsed;             // Game seconds since the crunch began.
        double traveled;            // Total commanded displacement, in meters.
        double logTimer;
        double windowCommanded;     // Commanded displacement since the last log line.
        Vector3d windowStartPos;
        CelestialBody windowBody;
        double lastToTarget = -1.0; // Distance left to the target at the last tick, or -1.

        GUIStyle hudStyle;

        void Awake() { instance = this; }
        void OnDestroy() { if (instance == this) instance = null; }

        static Vec3d V(Vector3d v) { return new Vec3d(v.x, v.y, v.z); }

        static double DropoutRadius(CelestialBody b)
        {
            double soi = b.sphereOfInfluence;
            if (double.IsInfinity(soi) || soi > 1e20)
                return b.Radius * CrunchTuning.RootBodyDropoutRadii;
            return soi * CrunchTuning.DropoutFraction;
        }

        // True when any operational engine is producing thrust.
        static bool EnginesThrusting(Vessel v)
        {
            foreach (ModuleEngines e in v.FindPartModulesImplementing<ModuleEngines>())
                if (e.isOperational && e.finalThrust > 0.01f)
                    return true;
            return false;
        }

        // World position of a target. Planets use their double-precision position.
        static bool TryGetTargetPosition(ITargetable t, out Vector3d pos)
        {
            pos = new Vector3d();
            if (t == null) return false;

            CelestialBody body = t as CelestialBody;
            if (body != null) { pos = body.position; return true; }

            Vessel tv = t as Vessel;
            if (tv != null) { pos = tv.GetWorldPos3D(); return true; }

            Transform tr = t.GetTransform();
            if (tr == null) return false;
            pos = (Vector3d)tr.position;
            return true;
        }

        static double AngleDeg(Vector3d a, Vector3d b)
        {
            double c = Vector3d.Dot(a.normalized, b.normalized);
            return System.Math.Acos(System.Math.Max(-1.0, System.Math.Min(1.0, c))) * 180.0 / System.Math.PI;
        }

        void ResetWindow(Vessel v)
        {
            windowCommanded = 0;
            windowStartPos = v.orbit.pos;
            windowBody = v.mainBody;
            logTimer = 0;
        }

        // ---- Button actions ----

        void DoPrimary()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            switch (phase)
            {
                case CrunchPhase.Idle: StartSpool(v); break;
                case CrunchPhase.Ready:
                    engageRequested = true;
                    Status = CrunchText.StatusWaitingAcceleration;
                    break;
                case CrunchPhase.Crunching: End(CrunchText.StopByWindow); break;
            }
        }

        void DoCancel()
        {
            if (phase == CrunchPhase.Spooling || phase == CrunchPhase.Ready || phase == CrunchPhase.Engaging)
                End(CrunchText.StopByWindow);
        }

        void StartSpool(Vessel v)
        {
            if (TimeWarp.CurrentRate > 1.0f) { Refuse(CrunchText.RefuseTimeWarp); return; }

            crunchVessel = v;
            spoolElapsed = 0;
            engageRequested = false;
            phase = CrunchPhase.Spooling;
            Status = CrunchText.StatusSpooling(0, CrunchTuning.SpoolSeconds);
            Post(CrunchText.MsgSpoolStart(CrunchTuning.SpoolSeconds));
        }

        void TryEngage(Vessel v)
        {
            if (TimeWarp.CurrentRate > 1.0f) { Refuse(CrunchText.RefuseTimeWarp); return; }
            if (FlightGlobals.VesselsLoaded.Count > 1) { Refuse(CrunchText.RefuseOtherVessels); return; }
            if (v.mainBody.atmosphere && v.altitude < v.mainBody.atmosphereDepth)
            { Refuse(CrunchText.RefuseAtmosphere); return; }

            Vector3d p = v.GetWorldPos3D();
            foreach (CelestialBody b in FlightGlobals.Bodies)
            {
                double r = DropoutRadius(b);
                if ((p - b.position).sqrMagnitude < r * r)
                { Refuse(CrunchText.RefuseZone(b.name)); return; }
            }

            Vector3d nose = ((Vector3d)v.ReferenceTransform.up).normalized;
            bool backwards = CrunchTuning.Backwards;
            Vector3d axis = backwards ? nose * -1.0 : nose;

            // With a target selected, crunch toward it instead of along the nose. The craft
            // only has to be roughly facing it (within the alignment tolerance).
            bool locked = false;
            ITargetable target = null;
            string targetName = "";
            Vector3d heading = axis;

            if (CrunchTuning.UseTargetLock)
            {
                target = v.targetObject;
                Vector3d tp;
                if (TryGetTargetPosition(target, out tp))
                {
                    Vector3d to = tp - p;
                    if (to.sqrMagnitude > 1.0)
                    {
                        Vector3d dir = to.normalized;
                        double err = AngleDeg(axis, dir);
                        if (err > CrunchTuning.TargetAlignToleranceDeg)
                        { Refuse(CrunchText.RefuseNotAligned(err, CrunchTuning.TargetAlignToleranceDeg)); return; }

                        locked = true;
                        heading = dir;
                        targetName = target.GetDisplayName().Replace("^N", "");
                        Debug.Log(string.Format("[CrunchDrive] Target lock: {0} distance={1:E3}m alignment={2:F2}deg",
                            targetName, to.magnitude, err));
                    }
                }
            }

            crunchBackwards = backwards;
            latchedHeading = heading;
            targetLocked = locked;
            lockedTarget = locked ? target : null;
            lockedTargetName = targetName;
            lastToTarget = -1.0;

            lastBodyName = v.mainBody.name;
            elapsed = 0;
            traveled = 0;
            ResetWindow(v);
            phase = CrunchPhase.Crunching;

            Debug.Log(string.Format("[CrunchDrive] Heading latched ({0:F4}, {1:F4}, {2:F4}) {3}{4}",
                latchedHeading.x, latchedHeading.y, latchedHeading.z, CrunchText.DirShort(crunchBackwards),
                locked ? " (target lock)" : ""));
            Post(CrunchText.MsgEngaged(CrunchText.DirShort(crunchBackwards)));
        }

        // ---- Per-tick logic ----

        void FixedUpdate()
        {
            if (phase == CrunchPhase.Idle) return;

            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || v != crunchVessel) { End(CrunchText.StopVesselChanged); return; }
            if (TimeWarp.CurrentRate > 1.0f) { End(CrunchText.StopTimeWarp); return; }

            double dt = TimeWarp.fixedDeltaTime;

            if (phase == CrunchPhase.Spooling)
            {
                spoolElapsed += dt;
                if (spoolElapsed >= CrunchTuning.SpoolSeconds)
                {
                    phase = CrunchPhase.Ready;
                    engageRequested = CrunchTuning.AutoEngage;
                    Status = engageRequested ? CrunchText.StatusWaitingAcceleration : CrunchText.StatusReady;
                    Post(CrunchText.MsgReady);
                }
                else
                {
                    Status = CrunchText.StatusSpooling(spoolElapsed, CrunchTuning.SpoolSeconds);
                }
                return;
            }

            if (phase == CrunchPhase.Ready)
            {
                if (engageRequested && EnginesThrusting(v))
                {
                    engageRequested = false;
                    if (CrunchTuning.EngageDelaySeconds > 0.0)
                    {
                        engageElapsed = 0;
                        phase = CrunchPhase.Engaging;
                        Status = CrunchText.StatusEngaging(CrunchTuning.EngageDelaySeconds);
                        Debug.Log(string.Format("[CrunchDrive] Engine thrust detected, entering field in {0:F1} s",
                            CrunchTuning.EngageDelaySeconds));
                    }
                    else
                    {
                        TryEngage(v);
                    }
                }
                else
                {
                    Status = engageRequested ? CrunchText.StatusWaitingAcceleration : CrunchText.StatusReady;
                }
                return;
            }

            if (phase == CrunchPhase.Engaging)
            {
                engageElapsed += dt;
                if (engageElapsed >= CrunchTuning.EngageDelaySeconds)
                {
                    // TryEngage moves to Crunching on success; a refusal leaves the drive Ready.
                    phase = CrunchPhase.Ready;
                    TryEngage(v);
                }
                else
                {
                    Status = CrunchText.StatusEngaging(CrunchTuning.EngageDelaySeconds - engageElapsed);
                }
                return;
            }

            // ---- Crunching ----
            if (FlightGlobals.VesselsLoaded.Count > 1) { End(CrunchText.StopOtherVessel); return; }

            double tier = CrunchTuning.UseInterstellarTier ? CrunchTuning.InterstellarTierC : CrunchTuning.InterplanetaryTierC;

            // Trim the final tick so the crunch ends exactly at CrunchSeconds.
            double stepDt = dt;
            bool lastTick = false;
            if (CrunchTuning.AutoEnd)
            {
                double remaining = CrunchTuning.CrunchSeconds - elapsed;
                if (remaining <= dt)
                {
                    stepDt = System.Math.Max(0.0, remaining);
                    lastTick = true;
                }
            }

            double dist = CrunchTuning.AutoEnd
                ? CrunchMath.SpringDisplacementPerTick(tier, elapsed, stepDt, CrunchTuning.CrunchSeconds,
                                                       CrunchTuning.SpringStiffness, CrunchTuning.SpringLaunch,
                                                       CrunchTuning.EndSpeedFraction)
                : CrunchMath.DisplacementPerTick(tier, stepDt);

            // Re-aim at the target every tick, so frame rotation or target motion can't bend the path.
            Vector3d p0 = v.GetWorldPos3D();
            Vector3d heading = latchedHeading;
            double toTarget = -1.0;
            if (targetLocked)
            {
                Vector3d tp;
                if (TryGetTargetPosition(lockedTarget, out tp))
                {
                    Vector3d to = tp - p0;
                    if (to.sqrMagnitude > 1.0)
                    {
                        toTarget = to.magnitude;
                        heading = to.normalized;
                        latchedHeading = heading;   // Keep the last good heading if the target disappears.
                    }
                }
            }

            // Sweep this tick's segment against every body's dropout sphere.
            Vector3d p1 = p0 + heading * dist;
            double bestT = 2.0;
            CelestialBody hit = null;
            foreach (CelestialBody b in FlightGlobals.Bodies)
            {
                double? t = CrunchMath.SweepEntry(V(p0), V(p1), V(b.position), DropoutRadius(b));
                if (t.HasValue && t.Value < bestT) { bestT = t.Value; hit = b; }
            }

            double moveDist = hit != null ? dist * bestT : dist;
            if (moveDist > 0.0) MoveAlong(v, heading * moveDist);

            elapsed += stepDt;
            traveled += moveDist;
            windowCommanded += moveDist;
            lastToTarget = toTarget >= 0.0 ? System.Math.Max(0.0, toTarget - moveDist) : -1.0;

            if (hit != null) { End(CrunchText.StopDropout(hit.name)); return; }
            if (lastTick) { End(CrunchText.StopDuration); return; }

            if (v.mainBody.name != lastBodyName)
            {
                Debug.Log("[CrunchDrive] Reference body changed: " + lastBodyName + " -> " + v.mainBody.name);
                lastBodyName = v.mainBody.name;
            }

            // Compare the ship's crunch axis with the current crunch heading.
            Vector3d nose = ((Vector3d)v.ReferenceTransform.up).normalized;
            Vector3d axis = crunchBackwards ? nose * -1.0 : nose;
            double offAxis = AngleDeg(axis, heading);

            // Live speed for the readout (the spring changes it throughout the crunch).
            double shownC = CrunchTuning.AutoEnd
                ? tier * CrunchMath.SpringSpeedFactor(
                    System.Math.Min(1.0, elapsed / CrunchTuning.CrunchSeconds),
                    CrunchTuning.SpringStiffness, CrunchTuning.SpringLaunch, CrunchTuning.EndSpeedFraction)
                : tier;

            string lockInfo = (targetLocked && lastToTarget >= 0.0)
                ? CrunchText.StatusTarget(lockedTargetName, lastToTarget / 1e9)
                : "";

            Status = CrunchText.StatusCrunching(
                crunchBackwards, shownC, elapsed, CrunchTuning.AutoEnd ? CrunchTuning.CrunchSeconds : -1.0,
                traveled / 1e9, offAxis, v.obt_velocity.magnitude, v.mainBody.name, lockInfo);

            logTimer += dt;
            if (logTimer >= 1.0)
            {
                string ratio = "n/a";
                if (windowBody == v.mainBody && windowCommanded > 0)
                    ratio = ((v.orbit.pos - windowStartPos).magnitude / windowCommanded).ToString("F3");
                Debug.Log(string.Format(
                    "[CrunchDrive] t={0:F1}s dir={1} commanded={2:E3}m achieved/commanded={3} offAxis={4:F2}deg body={5} realSpeed={6:F0}m/s toTarget={7:E3}m",
                    elapsed, CrunchText.DirShort(crunchBackwards),
                    windowCommanded, ratio, offAxis, v.mainBody.name, v.obt_velocity.magnitude, lastToTarget));
                ResetWindow(v);
            }
        }

        static void MoveAlong(Vessel v, Vector3d delta)
        {
            Vector3d target = (Vector3d)v.ReferenceTransform.position + delta;
            FloatingOrigin.SetOutOfFrameOffset(target);
        }

        void OnGUI()
        {
            if (phase == CrunchPhase.Idle) return;

            if (hudStyle == null)
            {
                hudStyle = new GUIStyle(GUI.skin.label);
                hudStyle.alignment = TextAnchor.UpperCenter;
                hudStyle.wordWrap = true;
                hudStyle.fontSize = 14;
                hudStyle.normal.textColor = Color.white;
            }

            // Wrap to the screen width and size the box to fit the text.
            float width = Mathf.Min(Screen.width - 40f, 1000f);
            GUIContent content = new GUIContent(Status);
            float height = hudStyle.CalcHeight(content, width);
            GUI.Label(new Rect((Screen.width - width) / 2f, 120f, width, height), content, hudStyle);
        }

        void Refuse(string reason) { Post(CrunchText.MsgRefused(reason)); }

        // Ends a spool, field entry or crunch and returns to idle.
        void End(string reason)
        {
            bool wasCrunching = phase == CrunchPhase.Crunching;
            phase = CrunchPhase.Idle;
            engageRequested = false;
            crunchVessel = null;
            targetLocked = false;
            lockedTarget = null;
            Status = CrunchText.StatusIdleBecause(reason);

            if (wasCrunching)
            {
                Debug.Log(string.Format("[CrunchDrive] Crunch summary: {0:F1} s, {1:E3} m commanded", elapsed, traveled));
                Post(CrunchText.MsgEnded(reason));
            }
            else
            {
                Post(CrunchText.MsgSpoolCancelled(reason));
            }
        }

        static void Post(string msg)
        {
            Debug.Log("[CrunchDrive] " + msg);
            ScreenMessages.PostScreenMessage(CrunchText.ScreenPrefix + msg, 4f, ScreenMessageStyle.UPPER_CENTER);
        }
    }
}