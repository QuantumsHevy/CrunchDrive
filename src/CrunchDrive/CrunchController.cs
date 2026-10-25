using CrunchDrive.Core;
using UnityEngine;

namespace CrunchDrive
{
    public enum CrunchPhase { Idle, Spooling, Ready, Crunching }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class CrunchController : MonoBehaviour
    {
        public static string Status = CrunchText.StatusIdle;
        public static string TargetLine = CrunchText.NoTarget;   // Distance readout for the selected target.
        public static double PreviewTierC = CrunchTuning.InterplanetaryTierC;   // Tier for the current target, for the window.
        static CrunchController instance;

        public static CrunchPhase CurrentPhase { get { return instance != null ? instance.phase : CrunchPhase.Idle; } }
        public static void PrimaryAction() { if (instance != null) instance.DoPrimary(); }
        public static void Cancel() { if (instance != null) instance.DoCancel(); }

        public static bool IsTurning { get { return instance != null && instance.turning; } }

        static double interstellarThreshold = -1.0;
        static readonly double SpringPeak = CrunchMath.SpringPeakFactor(
            CrunchTuning.SpringStiffness, CrunchTuning.SpringLaunch, CrunchTuning.EndSpeedFraction);

        // Everything a crunch needs, worked out (and validated) from the vessel's current state.
        struct CrunchPlan
        {
            public bool backwards;
            public bool locked;
            public bool interstellar;
            public bool twoLeg;
            public ITargetable target;
            public string targetName;
            public double targetDistance;
            public Vector3d heading;
            public double tier;
            public double duration;
            public double ramp;
        }

        CrunchPhase phase = CrunchPhase.Idle;
        bool crunchBackwards;
        Vessel crunchVessel;
        Vector3d latchedHeading;    // Fixed for the whole crunch unless a target lock re-aims it.
        string lastBodyName;

        ITargetable lockedTarget;   // Target the crunch is homing on, if any.
        bool targetLocked;
        string lockedTargetName = "";

        // Two-leg maneuver state. The phase stays Crunching for the whole maneuver, so the window's
        // button keeps working as a cancel while the craft coasts and turns between the legs.
        bool twoLeg;                // This crunch is a two-leg maneuver.
        int leg;                    // 1 = forward leg, 2 = backward leg.
        bool turning;               // True between the legs: coasting, SAS turning the craft, waiting for alignment.
        bool sasChanged;            // True once the drive has changed the player's SAS settings.
        bool sasWasOn;              // SAS state before the drive touched it.
        VesselAutopilot.AutopilotMode sasPrevMode;   // SAS mode before the drive touched it.
        bool sasFailLogged;
        double turnLogTimer;
        double turnSettled;         // Seconds the craft has been aligned and nearly still.
        readonly TurnController turnCtl = new TurnController();
        bool turnFallback;          // The attitude controller couldn't steer this craft; SAS does the turn instead.
        bool spoolInterstellar;     // The planned crunch is interstellar (decides whether warp is allowed while Ready).
        double lastUt = -1.0;       // Universal time at the last crunch tick, for crunches under time warp.

        Vessel arrivalVessel;       // Set after an arrival, until the arrival assistance has finished.
        double arrivalWait;         // Seconds spent waiting for KSP to switch the reference body.
        double arrivalDelay;        // Seconds spent settled in the new body's frame.
        Vessel arrivalTargetVessel; // The craft the finished crunch was homing on, if it was a craft.
        Vessel capTarget;           // Craft the assistance is steering an intercept with (null = plain capture).
        bool arrivalStarted;        // The arrival assistance has begun (capture and/or the retrograde turn).
        bool arrivalIntercept;      // The arrival is an intercept with a craft rather than a capture.
        bool arrivalSasWasOn;       // The player's SAS state before the drive touched it.
        bool arrivalSasTouched;     // The drive changed SAS during the maneuver (so it may need undoing).
        bool aimDone;               // The retrograde turn has finished (or isn't wanted).
        bool arrivalFallback;       // The attitude controller couldn't steer; SAS does the retrograde instead.
        double aimSettled;          // Seconds the craft has been retrograde and nearly still.
        double aimIdle;             // Seconds spent turning after capture assistance finished.

        bool capturing;             // True while capture assistance is changing the craft's velocity.
        CelestialBody capBody;      // Body the craft is being captured by.
        double capRate;             // Largest velocity change per second the ramp applies (m/s per s).
        double capElapsed;          // Seconds since capture assistance started.

        double spoolElapsed;        // Seconds spent spooling.
        double engineThrustTime;    // Seconds of continuous engine thrust seen while the drive is active.
        double activeTier;          // Average speed (c) locked in when the crunch engaged.
        bool activeInterstellar;    // True when this crunch uses the trapezoid curve instead of the spring.
        double activeDuration;      // Length of the current crunch leg, in seconds.
        double activeRamp;          // Interstellar ramp-up and ramp-down, each as a fraction of the leg.
        double elapsed;             // Game seconds since the current leg began.
        double traveled;            // Total commanded displacement, in meters.
        double logTimer;
        double windowCommanded;     // Commanded displacement since the last log line.
        Vector3d windowStartPos;
        CelestialBody windowBody;
        double lastToTarget = -1.0; // Distance left to the target at the last tick, or -1.

        void Awake() { instance = this; }
        void OnDestroy() { turnCtl.End(); if (instance == this) instance = null; }

        static Vec3d V(Vector3d v) { return new Vec3d(v.x, v.y, v.z); }

        // Far targets are those beyond a multiple of the distance from the root star to Eeloo (the outermost
        // stock planet). Falls back to the farthest planet if there is no Eeloo (a different planet pack).
        static double InterstellarThreshold()
        {
            if (interstellarThreshold <= 0.0)
            {
                CelestialBody sun = Planetarium.fetch.Sun;
                double reach = 0.0;
                string from = "Eeloo";
                foreach (CelestialBody b in FlightGlobals.Bodies)
                    if (b.name == "Eeloo" && b.orbit != null && b.referenceBody == sun) reach = b.orbit.semiMajorAxis;

                if (reach <= 0.0)
                {
                    from = "the outermost planet";
                    foreach (CelestialBody b in FlightGlobals.Bodies)
                        if (b != sun && !b.isStar && b.orbit != null && b.referenceBody == sun)
                            reach = System.Math.Max(reach, b.orbit.semiMajorAxis);
                }
                if (reach <= 0.0) return double.MaxValue;   // Not ready yet; treat everything as interplanetary.

                interstellarThreshold = reach * CrunchTuning.InterstellarEelooFactor;
                Debug.Log(string.Format("[CrunchDrive] Interstellar threshold: targets beyond {0:E3} m ({1:G3} x the distance to {2}).",
                    interstellarThreshold, CrunchTuning.InterstellarEelooFactor, from));
            }
            return interstellarThreshold;
        }

        static bool IsInterstellar(double targetDistance)
        {
            return targetDistance > InterstellarThreshold();
        }

        // Average speed (c) for a crunch toward a target this far away (0 = no target).
        // Clamped so the curve's PEAK never exceeds MaxSpeedC.
        public static double TierFor(double targetDistance)
        {
            if (IsInterstellar(targetDistance))
                return System.Math.Min(CrunchTuning.InterstellarTierC, CrunchTuning.MaxSpeedC);   // Rough figure for the window; legs are timed by LegTiming.
            return System.Math.Min(CrunchTuning.InterplanetaryTierC, CrunchTuning.MaxSpeedC / SpringPeak);
        }

        // Interstellar ramps last a fixed number of seconds (a fraction of the leg), not a share of its length.
        static double RampFor(double duration)
        {
            if (!(duration > 0.0)) return 0.5;
            return System.Math.Max(1e-6, System.Math.Min(0.5, CrunchTuning.InterstellarRampSeconds / duration));
        }

        // Average speed (c) for an interstellar leg with this ramp, so the PEAK stays under MaxSpeedC (0.1% margin).
        static double InterstellarTier(double ramp)
        {
            return System.Math.Min(CrunchTuning.InterstellarTierC, CrunchTuning.MaxSpeedC / CrunchMath.TrapezoidPeakFactor(ramp) * 0.999);
        }

        // Average speed, length and ramp of one leg that has to cover `travel` meters.
        static void LegTiming(bool interstellar, double travel, out double tier, out double duration, out double ramp)
        {
            if (!interstellar)
            {
                tier = System.Math.Min(CrunchTuning.InterplanetaryTierC, CrunchTuning.MaxSpeedC / SpringPeak);
                duration = CrunchMath.SecondsForDistance(travel, tier);
                ramp = 0.0;
                return;
            }

            // The ramp depends on the length and the length on the speed (which the ramp caps): settle it by iterating.
            tier = System.Math.Min(CrunchTuning.InterstellarTierC, CrunchTuning.MaxSpeedC);
            duration = CrunchMath.SecondsForDistance(travel, tier);
            ramp = RampFor(duration);
            for (int i = 0; i < 4; i++)
            {
                tier = InterstellarTier(ramp);
                duration = CrunchMath.SecondsForDistance(travel, tier);
                ramp = RampFor(duration);
            }
        }

        // Average speed and ramp of a leg whose length is fixed (Fixed-time mode, no target).
        static void FixedTiming(bool interstellar, double duration, out double tier, out double ramp)
        {
            if (!interstellar)
            {
                tier = System.Math.Min(CrunchTuning.InterplanetaryTierC, CrunchTuning.MaxSpeedC / SpringPeak);
                ramp = 0.0;
                return;
            }
            ramp = RampFor(duration);
            tier = InterstellarTier(ramp);
        }

        // Stars (the root star, and others like Anubis) get a multiple of their radius, never a share of their
        // sphere of influence: that would swallow the planets that orbit them.
        static double DropoutRadius(CelestialBody b)
        {
            double soi = b.isStar ? double.PositiveInfinity : b.sphereOfInfluence;
            double r = CrunchMath.DropoutRadius(
                soi, b.Radius, b.atmosphere ? b.atmosphereDepth : 0.0,
                CrunchTuning.DropoutFraction, CrunchTuning.MinDropoutAltitude, CrunchTuning.RootBodyDropoutRadii);
            if (b.isStar) r = System.Math.Max(r, b.Radius + (b.atmosphere ? b.atmosphereDepth : 0.0) + CrunchTuning.MinDropoutAltitude);
            return r;
        }

        // Distance to travel to reach a target's stopping point: its dropout edge for a planet or moon,
        // or a standoff distance for a vessel.
        static double StopDistance(ITargetable t, double distanceToCenter)
        {
            CelestialBody b = t as CelestialBody;
            double standoff = b != null ? DropoutRadius(b) : CrunchTuning.VesselStopDistance;
            return System.Math.Max(0.0, distanceToCenter - standoff);
        }

        // True when any operational engine is producing thrust.
        static bool EnginesThrusting(Vessel v)
        {
            foreach (ModuleEngines e in v.FindPartModulesImplementing<ModuleEngines>())
                if (e.isOperational && e.finalThrust > 0.01f)
                    return true;
            return false;
        }

        // Forces the throttle and RCS translation inputs to zero. The drive does this every frame
        // from the moment spooling starts until the field collapses.
        static void LockOutEngines(Vessel v)
        {
            v.ctrlState.mainThrottle = 0f;
            v.ctrlState.X = 0f;
            v.ctrlState.Y = 0f;
            v.ctrlState.Z = 0f;

            FlightInputHandler.state.mainThrottle = 0f;
            FlightInputHandler.state.X = 0f;
            FlightInputHandler.state.Y = 0f;
            FlightInputHandler.state.Z = 0f;
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

        void Update()
        {
            // Keep the engines locked out for as long as the drive is spooling, ready, or crunching.
            if (phase != CrunchPhase.Idle && crunchVessel != null && FlightGlobals.ActiveVessel == crunchVessel)
                LockOutEngines(crunchVessel);

            // Keep the target readout current whenever a target is selected, crunching or not.
            TargetLine = CrunchText.NoTarget;
            PreviewTierC = TierFor(0.0);

            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            ITargetable t = v.targetObject;
            Vector3d tp;
            if (!TryGetTargetPosition(t, out tp)) return;

            double distance = (tp - v.GetWorldPos3D()).magnitude;
            double stop = StopDistance(t, distance);
            PreviewTierC = TierFor(distance);
            string name = t.GetDisplayName().Replace("^N", "");

            TargetLine = CrunchText.StatusTargetIdle(name, stop, t is CelestialBody);

            // While crunching toward the target, the status readout already shows it; don't show it twice.
            if (phase == CrunchPhase.Crunching && !turning && targetLocked) TargetLine = "";
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
                case CrunchPhase.Ready: TryEngage(v); break;
                case CrunchPhase.Crunching: End(CrunchText.StopByWindow); break;   // Also cancels the turn between legs.
            }
        }

        void DoCancel()
        {
            if (phase == CrunchPhase.Spooling || phase == CrunchPhase.Ready)
                End(CrunchText.StopByWindow);
        }

        // ---- Checks ----

        // Works out whether a crunch can happen right now and builds its plan. On failure it posts the
        // reason and returns false. Used when spooling starts, so a doomed crunch never spins up, and
        // again at engage, because the ship can turn or drift during the spool.
        bool TryPlan(Vessel v, bool announce, out CrunchPlan plan)
        {
            plan = new CrunchPlan();

            if (EnginesThrusting(v)) { Refuse(CrunchText.RefuseEnginesActive); return false; }
            if (FlightGlobals.VesselsLoaded.Count > 1) { Refuse(CrunchText.RefuseOtherVessels); return false; }
            if (v.mainBody.atmosphere && v.altitude < v.mainBody.atmosphereDepth)
            { Refuse(CrunchText.RefuseAtmosphere); return false; }

            Vector3d p = v.GetWorldPos3D();
            foreach (CelestialBody b in FlightGlobals.Bodies)
            {
                double r = DropoutRadius(b);
                if ((p - b.position).sqrMagnitude < r * r)
                { Refuse(CrunchText.RefuseZone(b.name)); return false; }
            }

            Vector3d nose = ((Vector3d)v.ReferenceTransform.up).normalized;
            bool backwards = CrunchTuning.Backwards;
            Vector3d axis = backwards ? nose * -1.0 : nose;

            // With a target selected, crunch toward it instead of along the nose. The craft
            // only has to be roughly facing it (within the alignment tolerance).
            bool locked = false;
            string targetName = "";
            double targetDistance = 0.0;
            Vector3d heading = axis;

            ITargetable target = v.targetObject;
            Vector3d tp;
            if (TryGetTargetPosition(target, out tp))
            {
                Vector3d to = tp - p;
                if (to.sqrMagnitude > 1.0)
                {
                    Vector3d dir = to.normalized;
                    double err = AngleDeg(axis, dir);
                    if (err > CrunchTuning.TargetAlignToleranceDeg)
                    { Refuse(CrunchText.RefuseNotAligned(err, CrunchTuning.TargetAlignToleranceDeg)); return false; }

                    locked = true;
                    heading = dir;
                    targetDistance = to.magnitude;
                    targetName = target.GetDisplayName().Replace("^N", "");
                    if (announce)
                        Debug.Log(string.Format("[CrunchDrive] Target lock: {0} distance={1:E3}m alignment={2:F2}deg",
                            targetName, targetDistance, err));
                }
            }

            // Speed and curve: interplanetary or interstellar by target distance, capped so the peak stays legal.
            double planDistance = locked ? targetDistance : 0.0;
            bool interstellar = IsInterstellar(planDistance);

            // Time warp is only allowed for an interstellar crunch that is being engaged (not while spooling).
            if (TimeWarp.CurrentRate > 1.0f &&
                !(interstellar && CrunchTuning.AllowInterstellarWarp && phase == CrunchPhase.Ready))
            { Refuse(CrunchText.RefuseTimeWarp); return false; }

            // Any target (planet, moon or craft) gets the two-leg maneuver: the first leg covers half the distance
            // to the stopping point. The second leg is planned when it starts, from the real distance left.
            bool twoLegPlan = locked && CrunchTuning.TwoLegManeuver;

            // Crunch length: fixed, or worked out from the target distance at the same average speed.
            double tier, ramp;
            double duration = CrunchTuning.CrunchSeconds;
            if (twoLegPlan)
            {
                double leg1 = StopDistance(target, targetDistance) * 0.5;
                LegTiming(interstellar, leg1, out tier, out duration, out ramp);
                if (duration < CrunchTuning.MinCrunchSeconds) { Refuse(CrunchText.RefuseTooClose); return false; }

                if (announce)
                    Debug.Log(string.Format("[CrunchDrive] Two-leg maneuver: first leg {0:E3} m in {1:F2} s", leg1, duration));
            }
            else if (CrunchTuning.Mode != TravelMode.FixedDuration)
            {
                if (!locked) { Refuse(CrunchText.RefuseNoTarget); return false; }

                double travel = StopDistance(target, targetDistance);
                if (CrunchTuning.Mode == TravelMode.HalfDistance) travel *= 0.5;
                LegTiming(interstellar, travel, out tier, out duration, out ramp);
                if (duration < CrunchTuning.MinCrunchSeconds) { Refuse(CrunchText.RefuseTooClose); return false; }

                if (announce)
                    Debug.Log(string.Format("[CrunchDrive] Travel mode {0}: {1:E3} m in {2:F2} s",
                        CrunchTuning.Mode, travel, duration));
            }
            else
            {
                FixedTiming(interstellar, duration, out tier, out ramp);
            }

            if (announce)
                Debug.Log(string.Format("[CrunchDrive] {0} curve, compression coefficient {1:G4}c (peak {2:G4}c){3}",
                    interstellar ? "Trapezoid" : "Spring", tier,
                    tier * (interstellar ? CrunchMath.TrapezoidPeakFactor(ramp) : SpringPeak),
                    interstellar ? string.Format(", ramps {0:F1} s", ramp * duration) : ""));

            plan.backwards = backwards;
            plan.locked = locked;
            plan.interstellar = interstellar;
            plan.twoLeg = twoLegPlan;
            plan.target = locked ? target : null;
            plan.targetName = targetName;
            plan.targetDistance = targetDistance;
            plan.heading = heading;
            plan.tier = tier;
            plan.duration = duration;
            plan.ramp = ramp;
            return true;
        }

        void StartSpool(Vessel v)
        {
            // Nothing spins up, and the engines stay free, unless a crunch would actually be allowed.
            CrunchPlan plan;
            if (!TryPlan(v, false, out plan)) return;

            if (arrivalVessel != null) AbortArrival("a new spool started.");   // A new spool cancels any arrival assistance still waiting.
            capturing = false;
            turning = false;
            spoolInterstellar = plan.interstellar;
            crunchVessel = v;
            spoolElapsed = 0;
            engineThrustTime = 0;
            phase = CrunchPhase.Spooling;
            LockOutEngines(v);
            Status = CrunchText.StatusSpooling(0, CrunchTuning.SpoolSeconds);
            Post(CrunchText.MsgSpoolStart(CrunchTuning.SpoolSeconds));
        }

        void TryEngage(Vessel v)
        {
            // Re-check with fresh positions: the ship may have turned or drifted during the spool.
            CrunchPlan plan;
            if (!TryPlan(v, true, out plan)) return;

            activeTier = plan.tier;
            activeInterstellar = plan.interstellar;
            activeDuration = plan.duration;
            activeRamp = plan.ramp;
            crunchBackwards = plan.backwards;
            latchedHeading = plan.heading;
            targetLocked = plan.locked;
            lockedTarget = plan.target;
            lockedTargetName = plan.targetName;
            twoLeg = plan.twoLeg;
            leg = 1;
            turning = false;
            sasChanged = false;
            turnFallback = false;
            lastUt = -1.0;
            lastToTarget = -1.0;

            lastBodyName = v.mainBody.name;
            elapsed = 0;
            traveled = 0;
            ResetWindow(v);
            phase = CrunchPhase.Crunching;

            Debug.Log(string.Format("[CrunchDrive] Heading latched ({0:F4}, {1:F4}, {2:F4}) {3}{4}{5}",
                latchedHeading.x, latchedHeading.y, latchedHeading.z, CrunchText.DirShort(crunchBackwards),
                targetLocked ? " (target lock)" : "", twoLeg ? " (leg 1 of 2)" : ""));
            Post(CrunchText.MsgEngaged(CrunchText.DirShort(crunchBackwards)));
        }

        // ---- Between the legs: SAS turns the craft to face away from the target ----

        // Starts the turn at the end of the first leg. The craft keeps coasting on its own velocity.
        void BeginTurn(Vessel v)
        {
            turning = true;
            turnLogTimer = 0.0;
            turnSettled = 0.0;
            sasFailLogged = false;
            turnFallback = false;
            ReleaseWarp();

            if (!sasChanged)
            {
                sasWasOn = v.ActionGroups[KSPActionGroup.SAS];
                sasPrevMode = v.Autopilot != null ? v.Autopilot.Mode : VesselAutopilot.AutopilotMode.StabilityAssist;
                sasChanged = true;
            }

            Debug.Log(string.Format("[CrunchDrive] First leg complete after {0:E3} m. Turning to face away from {1}.",
                traveled, lockedTargetName));
            Post(CrunchText.MsgFirstLegDone);
        }

        // The turn needs the craft in physics, so any time warp is dropped.
        static void ReleaseWarp()
        {
            if (TimeWarp.CurrentRate > 1.0f)
            {
                TimeWarp.SetRate(0, true);
                Debug.Log("[CrunchDrive] " + CrunchText.MsgWarpDropped);
            }
        }

        static string StopAt(ITargetable t, string name)
        {
            CelestialBody b = t as CelestialBody;
            return b != null ? CrunchText.StopDropout(b.name) : CrunchText.StopNearVessel(name);
        }

        // Keeps SAS on and pointed at anti-target. Called every tick while turning, so it keeps trying if the
        // craft can't take the mode yet (or ever); the wait for alignment never gives up on its own.
        void HoldAntiTargetSas(Vessel v)
        {
            if (!v.ActionGroups[KSPActionGroup.SAS])
                v.ActionGroups.SetGroup(KSPActionGroup.SAS, true);

            VesselAutopilot autopilot = v.Autopilot;
            if (autopilot == null) return;
            if (autopilot.Mode == VesselAutopilot.AutopilotMode.AntiTarget) return;

            if (autopilot.CanSetMode(VesselAutopilot.AutopilotMode.AntiTarget))
            {
                autopilot.SetMode(VesselAutopilot.AutopilotMode.AntiTarget);
                sasFailLogged = false;
                Debug.Log("[CrunchDrive] SAS set to anti-target.");
            }
            else if (!sasFailLogged)
            {
                sasFailLogged = true;
                Debug.Log("[CrunchDrive] SAS cannot hold anti-target on this craft right now; will keep trying.");
            }
        }

        // Puts the player's SAS back the way it was before the turn.
        void RestoreSas(Vessel v)
        {
            if (!sasChanged) return;
            sasChanged = false;
            if (v == null || v.Autopilot == null) return;

            if (sasWasOn)
            {
                if (v.Autopilot.CanSetMode(sasPrevMode)) v.Autopilot.SetMode(sasPrevMode);
            }
            else
            {
                v.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
            }
            Debug.Log("[CrunchDrive] SAS restored to its previous setting.");
        }

        // One tick of the turn between the legs. Starts the second leg once the crunch axis (the tail)
        // points at the target within the alignment tolerance.
        void TickTurn(Vessel v, double dt)
        {
            ReleaseWarp();   // A player who warps during the turn just gets dropped back to 1x.

            Vector3d tp;
            if (!TryGetTargetPosition(lockedTarget, out tp)) { End(CrunchText.StopTargetLost); return; }

            Vector3d p0 = v.GetWorldPos3D();
            Vector3d to = tp - p0;
            double distance = to.magnitude;
            if (distance < 1.0) { End(CrunchText.StopDuration, true); return; }

            Vector3d dir = to * (1.0 / distance);

            // The second leg is backwards, so its crunch axis is the tail.
            Vector3d nose = ((Vector3d)v.ReferenceTransform.up).normalized;
            double err = AngleDeg(nose * -1.0, dir);
            Status = CrunchText.StatusTurning(lockedTargetName, err);

            // Still unpacking after time warp: nothing can steer yet.
            if (v.packed) { turnSettled = 0.0; return; }

            bool useController = CrunchTuning.UseTurnController && !turnFallback;
            if (useController)
            {
                if (turnCtl.Failed)
                {
                    turnFallback = true;
                    turnCtl.End();
                    useController = false;
                    Post(CrunchText.MsgTurnControllerFallback);
                }
                else
                {
                    // The controller steers with SAS off, so SAS can't fight it.
                    if (v.ActionGroups[KSPActionGroup.SAS]) v.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                    if (!turnCtl.Running) turnCtl.Begin(v);
                    turnCtl.DesiredNose = dir * -1.0;
                }
            }
            if (!useController) HoldAntiTargetSas(v);

            double rate = v.angularVelocity.magnitude * Mathf.Rad2Deg;   // Turning speed, degrees per second.

            turnLogTimer += dt;
            if (turnLogTimer >= 1.0)
            {
                turnLogTimer = 0.0;
                Debug.Log(string.Format("[CrunchDrive] Turning ({0}): crunch axis {1:F1} deg off the target, turning at {2:F2} deg/s.",
                    useController ? (turnCtl.Calibrated ? "controller" : "calibrating") : "SAS", err, rate));
            }

            // The controller's calibration pulses shake the craft; don't judge alignment until they're done.
            if (useController && !turnCtl.Calibrated) { turnSettled = 0.0; return; }

            // Start the second leg only once the craft is aligned AND has stopped turning for a moment, so
            // it isn't still swinging (or about to drift off) when the field forms.
            if (err <= CrunchTuning.TurnAlignToleranceDeg && rate <= CrunchTuning.TurnSettleRateDegPerSec)
                turnSettled += dt;
            else
                turnSettled = 0.0;
            if (turnSettled < CrunchTuning.TurnSettleSeconds) return;

            // Aligned: plan the second leg from the real distance left and start it.
            double remaining = StopDistance(lockedTarget, distance);
            if (remaining <= 0.0) { End(StopAt(lockedTarget, lockedTargetName), true); return; }

            bool interstellar2 = IsInterstellar(distance);
            double tier, duration, ramp;
            LegTiming(interstellar2, remaining, out tier, out duration, out ramp);
            if (duration < CrunchTuning.MinCrunchSeconds) { End(StopAt(lockedTarget, lockedTargetName), true); return; }

            // Hand the attitude to SAS for the second leg, so the craft holds its heading while the field is up.
            if (turnCtl.Running) { turnCtl.End(); HoldAntiTargetSas(v); }

            activeTier = tier;
            activeInterstellar = interstellar2;
            activeDuration = duration;
            activeRamp = ramp;
            crunchBackwards = true;
            latchedHeading = dir;
            elapsed = 0.0;
            lastUt = -1.0;
            leg = 2;
            turning = false;
            logTimer = 0.0;
            ResetWindow(v);

            Debug.Log(string.Format("[CrunchDrive] Aligned ({0:F2} deg, {1:F2} deg/s). Second leg: {2:E3} m in {3:F2} s, backwards.",
                err, rate, remaining, duration));
            Post(CrunchText.MsgSecondLeg);
        }

        // ---- Arrival: SAS retrograde and gradual capture ----

        // The non-root body whose sphere of influence the vessel is currently inside, or null.
        static CelestialBody InnermostSoi(Vessel v)
        {
            Vector3d p = v.GetWorldPos3D();
            CelestialBody best = null;
            foreach (CelestialBody b in FlightGlobals.Bodies)
            {
                double soi = b.sphereOfInfluence;
                if (double.IsInfinity(soi) || soi > 1e20) continue;
                if ((p - b.position).sqrMagnitude < soi * soi && (best == null || soi < best.sphereOfInfluence))
                    best = b;
            }
            return best;
        }

        // If the player has SAS on, points it at retrograde. Nothing rotates the craft by force: SAS
        // turns it at the craft's own pace. If SAS is off, SAS and the navball are left alone.
        // SAS retrograde follows the navball, so the navball goes to Orbit mode to match the orbital velocity
        // (or Target mode, when the assistance is steering an intercept with a craft).
        static void HoldRetrogradeWithSas(Vessel v, bool relativeToTarget)
        {
            if (!v.ActionGroups[KSPActionGroup.SAS]) return;

            VesselAutopilot autopilot = v.Autopilot;
            if (autopilot == null || !autopilot.CanSetMode(VesselAutopilot.AutopilotMode.Retrograde))
            {
                Debug.Log("[CrunchDrive] SAS cannot hold retrograde on this craft; leaving SAS alone.");
                return;
            }

            FlightGlobals.SetSpeedMode(relativeToTarget ? FlightGlobals.SpeedDisplayModes.Target : FlightGlobals.SpeedDisplayModes.Orbit);
            autopilot.SetMode(VesselAutopilot.AutopilotMode.Retrograde);
            Debug.Log("[CrunchDrive] SAS set to retrograde" + (relativeToTarget ? " (relative to the target)." : "."));
        }

        // Lowest altitude-safe radius for a periapsis around this body.
        static double SafePeriapsisRadius(CelestialBody b)
        {
            return b.Radius + (b.atmosphere ? b.atmosphereDepth : 0.0) + CrunchTuning.CaptureSafeAltitude;
        }

        // True when the craft is bound to the body (below escape speed) and its orbit's periapsis clears
        // the surface and atmosphere. r and vel are the craft's position and velocity relative to the body.
        static bool IsCaptured(CelestialBody b, Vector3d r, Vector3d vel, out double periapsisRadius)
        {
            periapsisRadius = 0.0;
            double rm = r.magnitude;
            double speed = vel.magnitude;
            if (!(rm > 1.0) || double.IsNaN(speed) || double.IsInfinity(speed)) return true;   // Nothing sane to correct.

            double mu = b.gravParameter;
            periapsisRadius = CrunchMath.PeriapsisRadius(mu, rm, speed, Vector3d.Cross(r, vel).magnitude);

            // A little slack above the target fraction so a ramp that lands on its target counts as done.
            bool bound = speed <= CrunchMath.EscapeSpeed(mu, rm) * (CrunchTuning.CaptureSpeedFraction + 0.005);
            bool clear = periapsisRadius >= SafePeriapsisRadius(b) - 1.0;
            return bound && clear;
        }

        // The velocity capture assistance steers toward: the same sideways direction the craft already has
        // (so it is redirected as little as possible), slowed to the bound limit if needed, and no slower
        // than the speed that keeps periapsis clear of the surface.
        static Vector3d CaptureTargetVelocity(CelestialBody b, Vector3d r, Vector3d vel)
        {
            double rm = r.magnitude;
            double speed = vel.magnitude;
            double mu = b.gravParameter;
            Vector3d rHat = r * (1.0 / rm);

            // Sideways direction of the current velocity. A dead-radial velocity has none; any direction around the body will do.
            Vector3d side = vel - rHat * Vector3d.Dot(rHat, vel);
            if (double.IsNaN(side.magnitude) || side.magnitude < 1e-3 * System.Math.Max(1.0, speed))
            {
                Vector3d any = System.Math.Abs(rHat.y) < 0.9 ? new Vector3d(0.0, 1.0, 0.0) : new Vector3d(1.0, 0.0, 0.0);
                side = Vector3d.Cross(rHat, any);
            }

            double minSpeed = CrunchMath.MinSpeedForPeriapsis(mu, rm, SafePeriapsisRadius(b)) * 1.02;
            double s = CrunchMath.CappedArrivalSpeed(System.Math.Max(speed, minSpeed), mu, rm, CrunchTuning.CaptureSpeedFraction);
            return side.normalized * s;
        }

        // Starts capture assistance if the craft is escaping or on a collision course. Returns true when it
        // took over (the ramp continues in StepCapture; an instant change finishes the arrival here).
        bool BeginCapture(Vessel v, CelestialBody body, Vector3d vel)
        {
            Vector3d r = v.GetWorldPos3D() - body.position;
            double rp;
            if (IsCaptured(body, r, vel, out rp))
            {
                Debug.Log(string.Format("[CrunchDrive] No capture assistance needed at {0}: bound, periapsis radius {1:E3} m.", body.name, rp));
                return false;
            }

            Vector3d target = CaptureTargetVelocity(body, r, vel);
            Vector3d dv = target - vel;
            capBody = body;
            capElapsed = 0.0;

            Debug.Log(string.Format("[CrunchDrive] Capture assistance at {0}: {1:F0} -> {2:F0} m/s (radius {3:E3} m, periapsis radius now {4:E3} m, direction change {5:F1} deg)",
                body.name, vel.magnitude, target.magnitude, r.magnitude, rp, AngleDeg(vel, target)));
            if (target.magnitude < vel.magnitude)
                Post(CrunchText.MsgCaptureAssist(vel.magnitude, target.magnitude, body.name));

            if (CrunchTuning.CaptureRampSeconds > 0.0)
            {
                capRate = dv.magnitude / CrunchTuning.CaptureRampSeconds;
                capturing = true;
                Debug.Log(string.Format("[CrunchDrive] Spreading the velocity change over about {0:F1} s.", CrunchTuning.CaptureRampSeconds));
            }
            else
            {
                v.ChangeWorldVelocity(dv);
                Debug.Log("[CrunchDrive] Velocity change applied.");
            }
            return true;
        }

        void EndCapture(string why)
        {
            Debug.Log("[CrunchDrive] Capture assistance finished: " + why);
            capturing = false;
            capTarget = null;
        }

        // ---- Intercept: capture assistance for a crunch that was homing on another craft ----

        // True when the finished crunch was homing on a craft that shares the vessel's reference body and is close enough.
        static bool CanIntercept(Vessel v, Vessel tv)
        {
            if (tv == null || tv == v || tv.orbit == null) return false;
            if (tv.mainBody != v.mainBody) return false;
            return (tv.GetWorldPos3D() - v.GetWorldPos3D()).magnitude <= CrunchTuning.InterceptMaxDistance;
        }

        // The craft's velocity relative to the target craft, and the relative velocity the intercept steers toward:
        // straight at the target, at a modest approach speed that drops as the distance closes.
        static void InterceptVelocities(Vessel v, Vessel tv, out Vector3d relNow, out Vector3d relWanted, out double distance)
        {
            Vector3d tvVel = tv.orbit.getOrbitalVelocityAtUT(Planetarium.GetUniversalTime()).xzy;   // World frame.
            relNow = v.obt_velocity - tvVel;

            Vector3d to = tv.GetWorldPos3D() - v.GetWorldPos3D();
            distance = to.magnitude;
            double speed = CrunchMath.InterceptSpeed(distance, CrunchTuning.InterceptApproachSpeed, CrunchTuning.InterceptMinSeconds);
            relWanted = distance > 1.0 ? to * (speed / distance) : new Vector3d(0.0, 0.0, 0.0);
        }

        static bool OnIntercept(Vector3d relNow, Vector3d relWanted, double distance)
        {
            if (distance <= 1.0) return true;
            double wanted = relWanted.magnitude;
            if (relNow.magnitude < 1e-3) return wanted < 1e-3;
            return AngleDeg(relNow, relWanted) < 1.0 && System.Math.Abs(relNow.magnitude - wanted) < System.Math.Max(5.0, 0.05 * wanted);
        }

        bool BeginIntercept(Vessel v, Vessel tv)
        {
            Vector3d relNow, relWanted;
            double distance;
            InterceptVelocities(v, tv, out relNow, out relWanted, out distance);
            if (OnIntercept(relNow, relWanted, distance))
            {
                Debug.Log("[CrunchDrive] No intercept assistance needed: already closing on the target.");
                return false;
            }

            Vector3d dv = relWanted - relNow;
            capTarget = tv;
            capElapsed = 0.0;
            string name = tv.GetDisplayName().Replace("^N", "");
            Debug.Log(string.Format("[CrunchDrive] Intercept assistance with {0}: relative velocity {1:F0} -> {2:F0} m/s, distance {3:E3} m, direction change {4:F1} deg",
                name, relNow.magnitude, relWanted.magnitude, distance, AngleDeg(relNow, relWanted)));
            Post(CrunchText.MsgIntercept(relNow.magnitude, relWanted.magnitude, name));

            if (CrunchTuning.CaptureRampSeconds > 0.0)
            {
                capRate = dv.magnitude / CrunchTuning.CaptureRampSeconds;
                capturing = true;
            }
            else
            {
                v.ChangeWorldVelocity(dv);
                capTarget = null;
            }
            return true;
        }

        void StepIntercept(Vessel v, double dt)
        {
            Vessel tv = capTarget;
            if (tv == null || tv.orbit == null || tv.mainBody != v.mainBody) { EndCapture("the target craft is no longer in the same reference body."); return; }

            Vector3d relNow, relWanted;
            double distance;
            InterceptVelocities(v, tv, out relNow, out relWanted, out distance);
            if (OnIntercept(relNow, relWanted, distance))
            {
                EndCapture(string.Format("on an intercept, closing at {0:F0} m/s, {1:E3} m away.", relNow.magnitude, distance));
                return;
            }

            capElapsed += dt;
            if (capElapsed > CrunchTuning.CaptureRampSeconds * 3.0 + 1.0)
            {
                EndCapture(string.Format("gave up after {0:F1} s.", capElapsed));
                return;
            }

            Vector3d need = relWanted - relNow;
            double maxStep = capRate * dt;
            double needMag = need.magnitude;
            v.ChangeWorldVelocity(needMag <= maxStep ? need : need * (maxStep / needMag));
        }

        // One tick of the ramp. The target velocity is recomputed from the craft's current position every
        // tick (the craft keeps drifting outward while it slows, which lowers the escape speed it has to
        // stay under), and the step toward it is limited to the ramp's rate. Stops as soon as the craft is
        // bound and clear of the surface.
        void StepCapture(Vessel v, double dt)
        {
            if (capTarget != null) { StepIntercept(v, dt); return; }

            CelestialBody body = capBody;
            if (body == null || v.mainBody != body) { EndCapture("the craft changed reference body."); return; }

            Vector3d r = v.GetWorldPos3D() - body.position;
            Vector3d vel = v.obt_velocity;
            double rp;
            if (IsCaptured(body, r, vel, out rp))
            {
                EndCapture(string.Format("bound at {0:F0} m/s, periapsis radius {1:E3} m.", vel.magnitude, rp));
                return;
            }

            capElapsed += dt;
            if (capElapsed > CrunchTuning.CaptureRampSeconds * 3.0 + 1.0)
            {
                EndCapture(string.Format("gave up after {0:F1} s.", capElapsed));
                return;
            }

            Vector3d need = CaptureTargetVelocity(body, r, vel) - vel;
            double maxStep = capRate * dt;
            double needMag = need.magnitude;
            v.ChangeWorldVelocity(needMag <= maxStep ? need : need * (maxStep / needMag));
        }

        void AbortArrival(string why)
        {
            Debug.Log("[CrunchDrive] Arrival assistance cancelled: " + why);

            // If the controller had SAS switched off, give it back.
            if (turnCtl.Running && arrivalSasWasOn && arrivalVessel != null && !arrivalVessel.ActionGroups[KSPActionGroup.SAS])
                arrivalVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
            turnCtl.End();

            arrivalVessel = null;
            arrivalStarted = false;
            capturing = false;
            capTarget = null;
        }

        // Everything the arrival needed to do is done.
        void FinishArrival()
        {
            turnCtl.End();
            arrivalVessel = null;
            arrivalStarted = false;
        }

        // The velocity "retrograde" is measured against: relative to the target craft on an intercept,
        // otherwise the orbital velocity around the reference body.
        Vector3d ArrivalVelocity(Vessel v)
        {
            if (arrivalIntercept && arrivalTargetVessel != null && arrivalTargetVessel.orbit != null && arrivalTargetVessel.mainBody == v.mainBody)
            {
                Vector3d relNow, relWanted;
                double distance;
                InterceptVelocities(v, arrivalTargetVessel, out relNow, out relWanted, out distance);
                return relNow;
            }
            return v.obt_velocity;
        }

        // Turns the craft retrograde. With the attitude controller this works with or without SAS: the controller
        // follows the changing velocity while capture assistance runs, then waits for the craft to settle and hands
        // over to SAS retrograde (only if the player had SAS on). Without it, SAS is simply set to retrograde.
        // Returns true while the turn is still in progress.
        bool AimRetrograde(Vessel v, double dt)
        {
            bool useController = CrunchTuning.UseTurnController && !arrivalFallback;
            if (useController && turnCtl.Failed)
            {
                arrivalFallback = true;
                turnCtl.End();
                useController = false;
                Post(CrunchText.MsgTurnControllerFallback);
            }

            if (!useController)
            {
                if (arrivalSasWasOn)
                {
                    if (!v.ActionGroups[KSPActionGroup.SAS]) v.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
                    HoldRetrogradeWithSas(v, arrivalIntercept);
                }
                else if (arrivalSasTouched && v.ActionGroups[KSPActionGroup.SAS])
                {
                    v.ActionGroups.SetGroup(KSPActionGroup.SAS, false);   // Undo the anti-target hold from the maneuver.
                }
                aimDone = true;
                return false;
            }

            Vector3d vel = ArrivalVelocity(v);
            if (vel.magnitude > 0.5)
            {
                if (v.ActionGroups[KSPActionGroup.SAS]) v.ActionGroups.SetGroup(KSPActionGroup.SAS, false);   // SAS would fight the controller.
                if (!turnCtl.Running) turnCtl.Begin(v);
                turnCtl.DesiredNose = vel * -1.0;   // Retrograde: the nose points against the velocity.
            }

            // Judge the turn only once capture assistance has stopped changing the velocity and calibration is over.
            if (capturing || !turnCtl.Calibrated) { aimSettled = 0.0; return true; }

            aimIdle += dt;
            Vector3d nose = ((Vector3d)v.ReferenceTransform.up).normalized;
            double err = AngleDeg(nose, vel * -1.0);
            double rate = v.angularVelocity.magnitude * Mathf.Rad2Deg;
            if (err <= CrunchTuning.TurnAlignToleranceDeg && rate <= CrunchTuning.TurnSettleRateDegPerSec)
                aimSettled += dt;
            else
                aimSettled = 0.0;

            if (aimSettled < CrunchTuning.TurnSettleSeconds && aimIdle < CrunchTuning.ArrivalAimTimeoutSeconds) return true;

            Debug.Log(string.Format("[CrunchDrive] Retrograde reached ({0:F2} deg off, {1:F2} deg/s){2}.", err, rate,
                aimSettled < CrunchTuning.TurnSettleSeconds ? " - gave up waiting for it to settle" : ""));
            turnCtl.End();
            if (arrivalSasWasOn)
            {
                v.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
                HoldRetrogradeWithSas(v, arrivalIntercept);
            }
            aimDone = true;
            return false;
        }

        // One tick of the arrival assistance once it has started: capture or intercept, and the retrograde turn.
        void TickArrival(Vessel v, double dt)
        {
            if (capturing) StepCapture(v, dt);
            bool aiming = !aimDone && AimRetrograde(v, dt);
            if (!capturing && !aiming) FinishArrival();
        }

        // Runs after an arrival. KSP needs a moment to re-base the vessel into the new body's frame, so
        // everything waits for the reference body to switch, then ArrivalDelaySeconds more. Then capture
        // assistance starts if the craft needs it, and the craft turns retrograde (no snap).
        void ProcessArrival(double dt)
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) { AbortArrival("no active vessel."); return; }
            if (v != arrivalVessel) { AbortArrival("the active vessel changed."); return; }
            if (phase != CrunchPhase.Idle) { AbortArrival("the drive was started again."); return; }
            if (v.packed) return;   // Still unpacking after time warp.

            if (arrivalStarted) { TickArrival(v, dt); return; }

            CelestialBody inside = InnermostSoi(v);
            if (inside == null || v.mainBody != inside)
            {
                arrivalWait += dt;
                arrivalDelay = 0.0;
                if (arrivalWait > CrunchTuning.ArrivalSettleSeconds)
                    AbortArrival("the craft never settled into a body's sphere of influence.");
                return;
            }

            arrivalDelay += dt;
            if (arrivalDelay < CrunchTuning.ArrivalDelaySeconds) return;

            Vector3d vel = v.obt_velocity;
            double speed = vel.magnitude;
            if (double.IsNaN(speed) || double.IsInfinity(speed) || speed < 1.0)
            {
                AbortArrival("no usable orbital velocity.");
                return;
            }

            arrivalStarted = true;
            aimDone = !CrunchTuning.SasRetrograde;
            arrivalFallback = false;
            aimSettled = 0.0;
            aimIdle = 0.0;

            // A crunch that was homing on a craft nearby gets an intercept instead of a capture.
            arrivalIntercept = CanIntercept(v, arrivalTargetVessel);
            if (CrunchTuning.CaptureAssist)
            {
                if (arrivalIntercept) BeginIntercept(v, arrivalTargetVessel);
                else BeginCapture(v, v.mainBody, vel);
            }

            TickArrival(v, dt);
        }

        // ---- Per-tick logic ----

        // Distance to move this tick along whichever curve the crunch uses.
        double CurveDisplacement(double stepDt)
        {
            if (activeInterstellar)
                return CrunchMath.TrapezoidDisplacementPerTick(activeTier, elapsed, stepDt, activeDuration,
                                                               activeRamp);
            return CrunchMath.SpringDisplacementPerTick(activeTier, elapsed, stepDt, activeDuration,
                                                        CrunchTuning.SpringStiffness, CrunchTuning.SpringLaunch,
                                                        CrunchTuning.EndSpeedFraction);
        }

        // Current speed as a multiple of the average tier, for the readout.
        double CurveSpeedFactor()
        {
            double progress = System.Math.Min(1.0, elapsed / activeDuration);
            if (activeInterstellar)
                return CrunchMath.TrapezoidSpeedFactor(progress, activeRamp);
            return CrunchMath.SpringSpeedFactor(progress, CrunchTuning.SpringStiffness, CrunchTuning.SpringLaunch,
                                                CrunchTuning.EndSpeedFraction);
        }

        // On-rails time warp is allowed only for interstellar crunches (experimental, see CrunchTuning).
        bool WarpAllowed()
        {
            if (!CrunchTuning.AllowInterstellarWarp) return false;
            if (phase == CrunchPhase.Ready) return spoolInterstellar;
            return phase == CrunchPhase.Crunching && activeInterstellar;
        }

        void FixedUpdate()
        {
            if (arrivalVessel != null) ProcessArrival(TimeWarp.fixedDeltaTime);

            if (phase == CrunchPhase.Idle) return;

            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || v != crunchVessel) { End(CrunchText.StopVesselChanged); return; }
            if (TimeWarp.CurrentRate > 1.0f && !WarpAllowed()) { End(CrunchText.StopTimeWarp); return; }

            double dt = TimeWarp.fixedDeltaTime;

            // Engines are locked out while the drive is active. If thrust still shows up (a solid
            // booster, say), the drive faults. A short grace period ignores single-frame blips.
            LockOutEngines(v);
            if (EnginesThrusting(v))
            {
                engineThrustTime += dt;
                if (engineThrustTime > CrunchTuning.EngineGraceSeconds) { End(CrunchText.StopEngineThrust); return; }
            }
            else
            {
                engineThrustTime = 0;
            }

            if (phase == CrunchPhase.Spooling)
            {
                spoolElapsed += dt;
                if (spoolElapsed >= CrunchTuning.SpoolSeconds)
                {
                    phase = CrunchPhase.Ready;
                    Status = CrunchText.StatusReady;
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
                Status = CrunchText.StatusReady;
                return;
            }

            // ---- Crunching ----
            if (FlightGlobals.VesselsLoaded.Count > 1) { End(CrunchText.StopOtherVessel); return; }

            // Between the legs the craft coasts while SAS turns it around.
            if (turning) { TickTurn(v, dt); return; }

            // Game seconds this tick: one physics step normally, more under time warp.
            double ut = Planetarium.GetUniversalTime();
            double gameDt = dt;
            if (TimeWarp.CurrentRate > 1.0f && lastUt >= 0.0)
                gameDt = System.Math.Max(dt, ut - lastUt);
            lastUt = ut;

            // Trim the final tick so the crunch ends exactly at the planned duration.
            double stepDt = gameDt;
            bool lastTick = false;
            double remaining = activeDuration - elapsed;
            if (remaining <= gameDt)
            {
                stepDt = System.Math.Max(0.0, remaining);
                lastTick = true;
            }

            double dist = CurveDisplacement(stepDt);

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
            // Distance left to the stopping point (the dropout zone edge for a planet or moon), not to the target's center.
            lastToTarget = toTarget >= 0.0 ? System.Math.Max(0.0, StopDistance(lockedTarget, toTarget) - moveDist) : -1.0;

            // Dropping out at a body's zone, or reaching a targeted planet or moon, counts as arriving.
            if (hit != null) { End(CrunchText.StopDropout(hit.name), true); return; }
            if (lastTick)
            {
                // End of the first leg: coast and turn instead of finishing.
                if (twoLeg && leg == 1) { BeginTurn(v); return; }
                End(CrunchText.StopDuration, targetLocked);
                return;
            }

            if (v.mainBody.name != lastBodyName)
            {
                Debug.Log("[CrunchDrive] Reference body changed: " + lastBodyName + " -> " + v.mainBody.name);
                lastBodyName = v.mainBody.name;
            }

            // Compare the ship's crunch axis with the current crunch heading.
            Vector3d nose = ((Vector3d)v.ReferenceTransform.up).normalized;
            Vector3d axis = crunchBackwards ? nose * -1.0 : nose;
            double offAxis = AngleDeg(axis, heading);

            // Live speed for the readout (both curves change it throughout the crunch).
            double shownC = activeTier * CurveSpeedFactor();

            string lockInfo = (targetLocked && lastToTarget >= 0.0)
                ? CrunchText.StatusTarget(lockedTargetName, lastToTarget, lockedTarget is CelestialBody)
                : "";

            Status = CrunchText.StatusCrunching(
                crunchBackwards, shownC, elapsed, activeDuration,
                traveled / 1e9, offAxis, v.obt_velocity.magnitude, v.mainBody.name, lockInfo);

            logTimer += dt;
            if (logTimer >= 1.0)
            {
                string ratio = "n/a";
                if (windowBody == v.mainBody && windowCommanded > 0)
                    ratio = ((v.orbit.pos - windowStartPos).magnitude / windowCommanded).ToString("F3");
                Debug.Log(string.Format(
                    "[CrunchDrive] t={0:F1}s leg={1} dir={2} commanded={3:E3}m achieved/commanded={4} offAxis={5:F2}deg body={6} realSpeed={7:F0}m/s toTarget={8:E3}m",
                    elapsed, leg, CrunchText.DirShort(crunchBackwards),
                    windowCommanded, ratio, offAxis, v.mainBody.name, v.obt_velocity.magnitude, lastToTarget));
                ResetWindow(v);
            }
        }

        // Under on-rails time warp the vessel is packed and the floating origin has no say in where it is,
        // so the displacement goes into its orbit instead. EXPERIMENTAL: not tested in-game.
        static void MoveOrbit(Vessel v, Vector3d delta)
        {
            Orbit o = v.orbit;
            double ut = Planetarium.GetUniversalTime();
            Vector3d pos = o.getRelativePositionAtUT(ut) + delta.xzy;   // The orbit frame has y and z swapped.
            Vector3d vel = o.getOrbitalVelocityAtUT(ut);
            o.UpdateFromStateVectors(pos, vel, o.referenceBody, ut);
            v.orbitDriver.pos = o.pos.xzy;
            v.orbitDriver.vel = o.vel;
        }

        static void MoveAlong(Vessel v, Vector3d delta)
        {
            if (v.packed) { MoveOrbit(v, delta); return; }
            Vector3d target = (Vector3d)v.ReferenceTransform.position + delta;
            FloatingOrigin.SetOutOfFrameOffset(target);
        }

        void Refuse(string reason) { Post(CrunchText.MsgRefused(reason)); }

        // Ends a spool or crunch, restores engine control and returns to idle. When the crunch ended
        // at its destination, arrival assistance (SAS retrograde, capture) is queued.
        void End(string reason, bool arrived = false)
        {
            bool wasCrunching = phase == CrunchPhase.Crunching;
            Vessel ended = crunchVessel;
            Vessel endedTarget = lockedTarget as Vessel;

            // The attitude controller always lets go, whatever the reason.
            turnCtl.End();

            // Arriving under time warp: drop to 1x so the arrival assistance has a craft in physics to work on.
            if (arrived) ReleaseWarp();

            bool sasOnBefore = sasChanged ? sasWasOn : (ended != null && ended.ActionGroups[KSPActionGroup.SAS]);
            bool sasTouched = sasChanged;

            // A cancelled or failed maneuver gives the player's SAS back. A finished one hands it to the arrival step.
            if (sasChanged)
            {
                if (arrived && CrunchTuning.SasRetrograde) sasChanged = false;
                else RestoreSas(ended);
            }

            phase = CrunchPhase.Idle;
            crunchVessel = null;
            targetLocked = false;
            lockedTarget = null;
            turning = false;
            twoLeg = false;
            Status = CrunchText.StatusIdleBecause(reason);

            if (wasCrunching)
            {
                Debug.Log(string.Format("[CrunchDrive] Crunch summary: {0:E3} m commanded", traveled));
                Post(CrunchText.MsgEnded(reason));

                if (arrived && ended != null && (CrunchTuning.CaptureAssist || CrunchTuning.SasRetrograde))
                {
                    arrivalVessel = ended;
                    arrivalTargetVessel = endedTarget;
                    arrivalSasWasOn = sasOnBefore;
                    arrivalSasTouched = sasTouched;
                    arrivalStarted = false;
                    capTarget = null;
                    arrivalWait = 0;
                    arrivalDelay = 0;
                    capturing = false;
                }
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