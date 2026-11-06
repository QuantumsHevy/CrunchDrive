using CrunchDrives.Core;

namespace CrunchDrives
{
    // Keep any {0} or {1:F1}-style placeholders in the same count and order. Status strings may contain "\n" as line breaks.
    public static class CrunchText
    {
        // Part (right-click menu and action groups)
        public const string GroupTitle = "Crunch Drive";
        public const string GroupTargetTitle = "Drive Targeting";
        public const string GroupSettingsTitle = "Drive Settings";
        public const string ModuleTitle = "Crunch Drive";
        public const string FieldStatus = "Status";
        public const string FieldCoefficient = "Crunch Coefficient";
        public const string FieldSpool = "Charge Time";
        public const string FieldLeg = "Leg";
        public const string FieldProgress = "Progress";
        public const string FieldSpeed = "Speed";
        public const string FieldTime = "Duration";
        public const string FieldTraveled = "Traveled";
        public const string FieldOffAxis = "Off-Axis";
        public const string FieldOrbital = "Relative Velocity";
        public const string FieldTarget = "Target";
        public const string FieldDistance = "Distance";
        public const string FieldRange = "Range";
        public const string FieldDraw = "Power Draw Rate (EC/s)";
        public const string FieldDropoutAltitude = "Altitude Limit";
        public const string FieldDropoutNearest = "Nearest Dropout";
        public const string FieldMode = "Mode";
        public const string ToggleOn = "On";
        public const string ToggleOff = "Off";
        public const string StatusStandby = "Standby";
        public const string ActionPrimary = "Crunch Field";
        public const string ModuleInfo = "Generates a Casimir-compressed gravity field in front of the craft. Range and compression depend on the power level and target.";
        public static string ButtonDirection(bool backwards) { return "Direction: " + DirLong(backwards); }

        public static string FieldCoefficientValue(double tierC) { return string.Format("{0:G3}c", tierC); }
        public static string FieldSpoolValue(double elapsed, double tierC)
        {
            return string.Format("{0:F1} s ({1:G3}c)", elapsed, tierC);
        }
        public static string FieldProgressValue(double elapsed, double duration) { return string.Format("{0:F0}%", duration > 0.0 ? 100.0 * elapsed / duration : 0.0); }
        public static string FieldSpeedValue(double tierC) { return string.Format("{0:G3}c ({1:N0} km/s)", tierC, tierC * CrunchMath.C / 1000.0); }
        public static string FieldOffAxisValue(double degrees) { return string.Format("{0:F1} degrees", degrees); }
        public static string FieldOrbitalValue(double metersPerSecond, string body) { return string.Format("{0:F0} m/s ({1})", metersPerSecond, body); }
        public static string FieldLegValue(int leg, bool backwards) { return string.Format("{0} of 2 ({1})", leg, DirHud(backwards)); }
        public static string FieldDropoutAltitudeValue(double meters, string body) { return FormatDistance(meters) + " above " + body; }
        public static string FieldDropoutNearestValue(string body, double edgeMeters)
        {
            if (edgeMeters < 0.0) return "Inside " + body + "'s dropout zone, " + FormatDistance(-edgeMeters) + " until departure.";
            return body + ": " + FormatDistance(edgeMeters) + " away.";
        }

        public static string FieldTimeValue(double elapsed, double duration) { return FormatDuration(elapsed) + " / " + FormatDuration(duration); }
        public static string FieldRangeValue(double seconds, double tierC) { return FormatDistance(tierC * CrunchMath.C * seconds); }

        public static string PhaseWord(CrunchPhase phase, bool turning)
        {
            if (phase == CrunchPhase.Crunching && turning) return "TURNING...";
            switch (phase)
            {
                case CrunchPhase.Spooling: return "SPOOLING...";
                case CrunchPhase.Ready: return "READY";
                case CrunchPhase.Crunching: return "ACTIVE";
                default: return "IDLE";
            }
        }

        // Debug Window
        public const string WindowTitle = "CRUNCHDRIVE DEBUG";
        public const string ButtonSpooling = "SPINNING UP CASIMIR BLADES...";
        public const string ButtonStop = "COLLAPSE CRUNCH FIELD";
        public const string ButtonAbortApproach = "ABORT APPROACH";
        public const string ButtonCancel = "STOP CASIMIR GENERATION";
        public const string SliderDuration = "Fixed-time Field Duration (s)";
        public const string LabelTier = "Crunch Coefficient: {0:G3}c";

        public static readonly string[] ModeNames = { "Full-Distance", "Half-Distance", "Fixed-Time" };

        // Settings (part menu)
        public const string ToggleCaptureAssist = "Use Capture Assistance";
        public const string ToggleSasRetrograde = "Use Capture Facilitation";
        public const string ToggleTurnController = "Use MechJeb2-Derived Attitude Controller";
        public const string ToggleCrunchWarp = "Allow time warp (EXPERIMENTAL)";
        public const string ToggleTwoLeg = "Use Automatic Approach";

        public static string DirLong(bool backwards) { return backwards ? "AFT" : "FORE"; }         // buttons
        public static string DirShort(bool backwards) { return backwards ? "BCK" : "FWD"; }         // messages and log
        public static string DirHud(bool backwards) { return backwards ? "BACKWARD" : "FORWARD"; }  // status readout

        public static string PrimaryButton(CrunchPhase phase, bool backwards, bool turning = false, bool canEngage = false)
        {
            if (phase == CrunchPhase.Crunching && turning) return ButtonAbortApproach;
            switch (phase)
            {
                case CrunchPhase.Spooling: return canEngage ? "ENGAGE CRUNCH DRIVE (" + DirLong(backwards) + ")" : ButtonSpooling;
                case CrunchPhase.Ready: return "ENGAGE CRUNCH DRIVE (" + DirLong(backwards) + ")";
                case CrunchPhase.Crunching: return ButtonStop;
                default: return "START CASIMIR GENERATION (" + DirLong(backwards) + ")";
            }
        }

        public static string FormatDistance(double meters)
        {
            const double au = 1.495978707e11;
            const double ly = 9.4607304725808e15;
            if (meters < 1e6) return string.Format("{0:F1} km", meters / 1e3);
            if (meters < 1e9) return string.Format("{0:F2} Mm", meters / 1e6);
            if (meters < 1e12) return string.Format("{0:F3} Gm", meters / 1e9);
            if (meters < 0.0125 * ly) return string.Format("{0:F2} AU", meters / au);
            return string.Format("{0:F3} ly", meters / ly);
        }

        public static string FormatDuration(double s)
        {
            if (s < 120.0) return string.Format("{0:F1}s", s);
            if (s < 7200.0) return string.Format("{0:F1}m", s / 60.0);
            if (s < 172800.0) return string.Format("{0:F1}h", s / 3600.0);
            return string.Format("{0:F1}d", s / 86400.0);
        }

        // Status Readout
        public const string StatusIdle = "IDLE";
        public static string StatusIdleBecause(string reason) { return "IDLE (" + reason + ")"; }

        public static string StatusSpooling(double elapsed, double tierC, double ecPerSecond, bool canEngage)
        {
            return string.Format("SPOOLING\nCASIMIR BLADES SPINNING UP.\n{0:F1} s ({1:G3}c)\nDRAW: {2:N0} EC/s.\n", elapsed, tierC, ecPerSecond)
                 + (canEngage ? "Engage when the coefficient is sufficient.\n" : "")
                 + "Throttle and translation are locked out.";
        }

        public const string StatusReady = "READY\nNEGATIVE ENERGY THRESHOLD REACHED.\nWAITING FOR ENGAGEMENT.";

        // lockInfo is either empty or starts with "\n" (see StatusTarget).
        public static string StatusCrunching(bool backwards, double tierC, double elapsed, double duration,
                                             double gm, double offAxisDeg, double realSpeed, string body, string lockInfo)
        {
            return string.Format(
                "CRUNCH FIELD ACTIVE ({0})\n" +
                "Speed: {1:G3}c ({2:N0} km/s)\n" +
                "Time: {3} / {4}\n" +
                "Traveled: {5:F3} Gm\n" +
                "Off-Axis: {6:F1} degrees\n" +
                "Orbital Velocity: {7:F0} m/s relative to {8}{9}",
                DirHud(backwards), tierC, tierC * CrunchMath.C / 1000.0, FormatDuration(elapsed), FormatDuration(duration),
                gm, offAxisDeg, realSpeed, body, lockInfo);
        }

        public static string StatusTurning(string target, double offDeg)
        {
            return string.Format("TURNING\nFIELD INACTIVE.\nTRAILING REGION OFFSET: {1:F0}° FROM {0}.", target, offDeg);
        }

        public static string StatusTarget(string name, double meters, bool isBody)
        {
            return string.Format("\nTARGET: {0}\n{1}", name, DistanceToStop(meters, isBody));
        }

        // Shown whenever a target is selected, even when no crunch is running.
        public const string NoTarget = "No target selected.";

        public static string StatusTargetIdle(string name, double stopMeters, bool isBody)
        {
            return string.Format("TARGET: {0}\n{1}", name, DistanceToStop(stopMeters, isBody));
        }

        // Distance still to cover: to a planet or moon's dropout zone edge, or to a craft's stopping point.
        public static string DistanceToStop(double meters, bool isBody)
        {
            return FormatDistance(meters) + (isBody ? " until dropout." : " to dropout.");
        }

        // Pop-Up Messages
        public const string ScreenPrefix = "[CrunchDrive] ";
        public static string MsgSpoolStart(double seconds)
        {
            return string.Format("Spooling Casimir blades... ({0:F1} s).", seconds);
        }
        public const string MsgSpoolStartOpen = "Spooling Casimir blades...";
        public static string MsgSpoolDry(double tierC)
        {
            return string.Format("Electric charge ran dry. Coefficient achieved: {0:G3}c.", tierC);
        }
        public static string MsgSpoolFull(double tierC)
        {
            return string.Format("Casimir blades at full speed. Coefficient achieved: {0:G3}c.", tierC);
        }
        public static string MsgEngaged(string dir) { return "Crunch field engaged (" + dir + ")."; }
        public static string MsgRefused(string reason) { return "Crunch refused: " + reason; }
        public static string MsgEnded(string reason) { return "Crunch ended: " + reason + " Engine controls restored."; }
        public static string MsgSpoolCancelled(string reason) { return "Spool cancelled: " + reason + " Engine controls restored."; }
        public static string MsgCaptureAssist(double fromSpeed, double toSpeed, string body)
        {
            return string.Format("Capture assistance reduced orbital velocity from {0:N0} to {1:N0} m/s relative to {2}.", fromSpeed, toSpeed, body);
        }
        public static string MsgIntercept(double fromSpeed, double toSpeed, string target)
        {
            return string.Format("Capture assistance set an intercept with {2}. Relative velocity changed from {0:N0} to {1:N0} m/s.", fromSpeed, toSpeed, target);
        }
        public const string MsgTurnControllerFallback = "Attitude controller found no steering torque. Switching to SAS...";
        public const string MsgWarpDropped = "Time warp released.";
        public const string MsgFirstLegDone = "Leg 1 complete. Turning...";
        public const string MsgSecondLeg = "Starting leg 2...";

        // Reasons
        public const string RefuseNoDrive = "No crunch drive is installed.";
        public const string RefuseTimeWarp = "Time warp is active.";
        public const string RefuseOtherVessels = "Another craft is loaded nearby.";
        public const string RefuseAtmosphere = "The craft is inside an atmosphere.";
        public const string RefuseEnginesActive = "Reaction engines are active.";
        public static string RefuseZone(string body) { return "The craft is inside " + body + "'s dropout zone."; }
        public static string RefuseNotAligned(double errDeg, double tolDeg)
        {
            return string.Format("The craft is pointed {0:F1} degrees off-target (limit {1:F1}).", errDeg, tolDeg);
        }
        public const string RefuseNoTarget = "A target is required for this travel mode.";
        public const string RefuseTooClose = "The target is too close to compress a field.";

        public const string StopByPlayer = "Stopped by the player.";
        public const string StopDriveLost = "The crunch drive was lost.";
        public const string StopVesselChanged = "The active craft changed.";
        public const string StopTimeWarp = "Time warp was engaged.";
        public const string StopOtherVessel = "Another craft entered the physics range.";
        public const string StopEngineThrust = "Engine vibrations have unbalanced the Casimir blades.";
        public const string StopTargetLost = "The target was lost.";
        public const string StopDuration = "The field collapsed naturally.";
        public static string StopNoCharge(double drawn, double rate)
        {
            return string.Format("Electric charge ran dry before any operating speed was reached. {0:N0} EC drawn at {1:N0} EC/s.", drawn, rate);
        }
        public static string StopNearVessel(string vessel) { return "The crunch ended near " + vessel + "."; }
        public static string StopDropout(string body) { return "The crunch ended near " + body + "."; }

        // Debug Logs
        public const string LogBootstrapPassed = "[CrunchDrive] Kerbal Space Program launched successfully with a CrunchDrive build.";
        public static string LogFlightSceneEntered(string vessel)
        {
            return string.Format("[CrunchDrive] {0} has entered the flight scene.", vessel);
        }
        public const string LogNoGlowMaterial = "[CrunchDrive] No emissives found on part.";
        public static string LogGlowMaterials(int materials, int renderers)
        {
            return string.Format("[CrunchDrive] {0} material(s) of {1} included renderer(s) found on part.", materials, renderers);
        }
        public const string LogNoTubeShader = "[CrunchDrive] No particle shader found.";
        public const string LogTubeAdditive = "[CrunchDrive] Additive shader found (parts may show through FX).";
        public static string LogTubeMeasured(double radius, double forward, double backward, double craftLength)
        {
            return string.Format("[CrunchDrive] FX MEASURE: radius {0:F1} m, fore length {1:F1} m, aft length {2:F1} m, total craft length {3:F1} m.", radius, forward, backward, craftLength);
        }
        public static string LogTubeSources(int renderers, int parts, string forwardPart, double forwardAlong, double forwardRadial,
                                            string backwardPart, double backwardAlong, double backwardRadial)
        {
            return string.Format("[CrunchDrive] FX measured from {0} mesh renderers on {1} parts. FWD end set by {2} ({3:F1} m along, {4:F1} m from axis). BWD end set by {5} ({6:F1} m along, {7:F1} m from axis).",
                renderers, parts, forwardPart, forwardAlong, forwardRadial, backwardPart, backwardAlong, backwardRadial);
        }
        public static string LogTubePoking(int corners)
        {
            return "[CrunchDrive] " + corners + " corners of this craft are wider than FX.";
        }
    }
}