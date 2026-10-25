using CrunchDrive.Core;

namespace CrunchDrive
{
    // Keep any {0} or {1:F1}-style placeholders in the same count and order. Status strings may contain "\n" as line breaks.
    public static class CrunchText
    {
        // Debug Logs
        public const string LogBootstrapPassed = "[CrunchDrive] Kerbal Space Program has launched with a CrunchDrive build successfully!";
        public static string LogFlightSceneEntered(string vesselName)
        {
            return "[CrunchDrive] " + vesselName + " has entered the flight scene with a CrunchDrive build.";
        }

        // Debug Window
        public const string WindowTitle = "CRUNCHDRIVE DEBUG";
        public const string ButtonSpooling = "SPINNING UP CASIMIR BLADES...";
        public const string ButtonStop = "COLLAPSE CRUNCH FIELD";
        public const string ButtonAbortApproach = "ABORT APPROACH";
        public const string ButtonCancel = "STOP CASIMIR GENERATION";
        public const string ToggleBackwards = "Crunch trailing region (travel backwards)";
        public const string SliderDuration = "Fixed Crunch Field Duration (s)";
        public const string LabelTier = "Compression Coefficient: {0:G3}c (set by target distance)";

        public static readonly string[] ModeNames = { "Full-distance", "Half-distance", "Fixed-time" };

        public const string TwoLegHint = "Automatic approach is on: planets, moons and craft are crunched in two legs that plan their own distances. The travel mode below applies to other targets, or to no target.";

        public static string ModeHint(TravelMode mode)
        {
            switch (mode)
            {
                case TravelMode.HalfDistance:
                    return "The field duration is calculated from half of the distance to the target's dropout zone.";
                case TravelMode.FullDistance:
                    return "The field duration is calculated from the entire distance to the target's dropout zone.";
                default:
                    return "";
            }
        }

        // Debug Window (toggles)
        public const string ToggleCaptureAssist = "Capture assistance (slow and redirect until bound)";
        public const string ToggleSasRetrograde = "Point the craft retrograde on arrival";
        public const string ToggleTurnController = "Steer with the attitude controller (otherwise SAS)";
        public const string ToggleInterstellarWarp = "Allow time warp during interstellar crunches (experimental)";
        public const string ToggleTwoLeg = "Automatic approach (crunch forward halfway, turn, crunch the trailing region the rest)";

        public static string DirLong(bool backwards) { return backwards ? "AFT" : "FORE"; }         // buttons
        public static string DirShort(bool backwards) { return backwards ? "BCK" : "FWD"; }         // messages and log
        public static string DirHud(bool backwards) { return backwards ? "BACKWARD" : "FORWARD"; }  // status readout

        public static string PrimaryButton(CrunchPhase phase, bool backwards, bool turning = false)
        {
            if (phase == CrunchPhase.Crunching && turning) return ButtonAbortApproach;
            switch (phase)
            {
                case CrunchPhase.Spooling: return ButtonSpooling;
                case CrunchPhase.Ready: return "ENGAGE COMPRESSION (" + DirLong(backwards) + ")";
                case CrunchPhase.Crunching: return ButtonStop;
                default: return "START CASIMIR GENERATION (" + DirLong(backwards) + ")";
            }
        }

        public static string DistanceInfo(double seconds, double tierC)
        {
            double meters = tierC * CrunchMath.C * seconds;
            return string.Format("A {0:F1} s transition at {1:G3}c covers {2:G4} Gm ({3:G3} AU, {4:G3} ly).",
                seconds, tierC, meters / 1e9, meters / 1.495978707e11, meters / 9.4607304725808e15);
        }

        public static string FormatDistance(double meters)
        {
            const double au = 1.495978707e11;
            const double ly = 9.4607304725808e15;
            if (meters < 1e6) return string.Format("{0:F1} km", meters / 1e3);
            if (meters < 1e9) return string.Format("{0:F2} Mm", meters / 1e6);
            if (meters < 1e12) return string.Format("{0:F3} Gm", meters / 1e9);
            if (meters < 0.1 * ly) return string.Format("{0:F2} AU", meters / au);
            return string.Format("{0:F3} ly", meters / ly);
        }

        public static string FormatDuration(double s)
        {
            if (s < 120.0) return string.Format("{0:F1} s", s);
            if (s < 7200.0) return string.Format("{0:F1} min", s / 60.0);
            if (s < 172800.0) return string.Format("{0:F1} h", s / 3600.0);
            return string.Format("{0:F1} d", s / 86400.0);
        }

        // Status Readout
        public const string StatusIdle = "IDLE";
        public static string StatusIdleBecause(string reason) { return "IDLE (" + reason + ")"; }

        public static string StatusSpooling(double elapsed, double total)
        {
            return string.Format("SPOOLING\nCasimir blades are spinning up.\n{0:F1} / {1:F1} s\nThrottle and translation have been locked out.", elapsed, total);
        }

        public const string StatusReady = "READY\nEnough negative energy has been accumulated for compression.\nWaiting for engagement.";

        // lockInfo is either empty or starts with "\n" (see StatusTarget).
        public static string StatusCrunching(bool backwards, double tierC, double elapsed, double duration,
                                             double gm, double offAxisDeg, double realSpeed, string body, string lockInfo)
        {
            return string.Format(
                "CRUNCH FIELD ACTIVE ({0})\n" +
                "Speed: {1:G3}c ({2:N0} km/s)\n" +
                "Time: {3} / {4}\n" +
                "Traveled: {5:F3} Gm\n" +
                "Off-axis: {6:F1} degrees\n" +
                "Orbital velocity: {7:F0} m/s relative to {8}{9}",
                DirHud(backwards), tierC, tierC * CrunchMath.C / 1000.0, FormatDuration(elapsed), FormatDuration(duration),
                gm, offAxisDeg, realSpeed, body, lockInfo);
        }

        public static string StatusTurning(string target, double offDeg)
        {
            return string.Format("TURNING\nCrunch field is down. Coasting.\nTrailing region is {1:F0} degrees off {0}.\nThe second crunch field forms once aligned.", target, offDeg);
        }

        public static string StatusTarget(string name, double meters, bool isBody)
        {
            return string.Format("\nTarget: {0}\n{1}", name, DistanceToStop(meters, isBody));
        }

        // Shown whenever a target is selected, even when no crunch is running.
        public const string NoTarget = "No target selected.";

        public static string StatusTargetIdle(string name, double stopMeters, bool isBody)
        {
            return string.Format("Target: {0}\n{1}", name, DistanceToStop(stopMeters, isBody));
        }

        // Distance still to cover: to a planet or moon's dropout zone edge, or to a craft's stopping point.
        public static string DistanceToStop(double meters, bool isBody)
        {
            return FormatDistance(meters) + (isBody ? " to dropout zone edge." : " to stopping point.");
        }

        // Pop-Up Messages
        public const string ScreenPrefix = "[CrunchDrive] ";
        public static string MsgSpoolStart(double seconds)
        {
            return string.Format("Casimir blades are spinning up ({0:F1} s).", seconds);
        }
        public const string MsgReady = "Enough negative energy has been accumulated for compression. Ready to engage.";
        public static string MsgEngaged(string dir) { return "Crunch field formed (" + dir + ")."; }
        public static string MsgRefused(string reason) { return "Crunch refused: " + reason; }
        public static string MsgEnded(string reason) { return "Crunch ended: " + reason + " Engine controls restored."; }
        public static string MsgSpoolCancelled(string reason) { return "Spool cancelled: " + reason + " Engine controls restored."; }
        public static string MsgCaptureAssist(double fromSpeed, double toSpeed, string body)
        {
            return string.Format("Capture assistance reduced orbital velocity from {0:N0} to {1:N0} m/s relative to {2}.", fromSpeed, toSpeed, body);
        }
        public static string MsgIntercept(double fromSpeed, double toSpeed, string target)
        {
            return string.Format("Capture assistance is putting the craft on an intercept with {2}: relative velocity {0:N0} to {1:N0} m/s.", fromSpeed, toSpeed, target);
        }
        public const string MsgTurnControllerFallback = "The attitude controller found no torque to steer with. Falling back to SAS.";
        public const string MsgWarpDropped = "Time warp was released.";
        public const string MsgFirstLegDone = "Halfway point reached. Crunch field is down. Turning the craft to aim its trailing region at the target.";
        public const string MsgSecondLeg = "Trailing region aligned with the target. Second crunch field formed.";

        // Reasons
        public const string RefuseTimeWarp = "Time warp is active.";
        public const string RefuseOtherVessels = "Other craft are loaded nearby.";
        public const string RefuseAtmosphere = "The craft is inside an atmosphere.";
        public const string RefuseEnginesActive = "Reaction engines are active.";
        public static string RefuseZone(string body) { return "The craft is inside " + body + "'s dropout zone."; }
        public static string RefuseNotAligned(double errDeg, double tolDeg)
        {
            return string.Format("The craft is pointed {0:F1} degrees off its target (limit {1:F1}).", errDeg, tolDeg);
        }
        public const string RefuseNoTarget = "This travel mode needs a selected target.";
        public const string RefuseTooClose = "The target is too close to form a crunch field.";

        public const string StopByWindow = "Stopped by debug window.";
        public const string StopVesselChanged = "The active craft changed.";
        public const string StopTimeWarp = "Time warp was engaged.";
        public const string StopOtherVessel = "Another craft entered physics range.";
        public const string StopEngineThrust = "Engine vibrations unbalanced the Casimir blades.";
        public const string StopTargetLost = "The target was lost.";
        public const string StopDuration = "The field collapsed naturally.";
        public static string StopNearVessel(string vessel) { return "The craft left crunch near " + vessel + "."; }
        public static string StopDropout(string body) { return "The craft left crunch near " + body + "'s dropout zone."; }
    }
}