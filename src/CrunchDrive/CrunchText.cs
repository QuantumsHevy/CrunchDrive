using CrunchDrive.Core;

namespace CrunchDrive
{
    // Keep any {0}, {1:F1}-style placeholders in the same count and order.
    // Status strings may contain "\n"; the window and the on-screen readout both wrap.
    public static class CrunchText
    {
        // Debug Logs
        public const string LogBootstrapPassed = "[CrunchDrive] Kerbal Space Program has launched with a CrunchDrive build successfully!";
        public static string LogFlightSceneEntered(string vesselName)
        {
            return "[CrunchDrive] " + vesselName + " has entered the flight scene.";
        }

        // Debug Window
        public const string WindowTitle = "CRUNCHDRIVE DEBUG";
        public const string ButtonSpooling = "SPOOLING...";
        public const string ButtonEngaging = "ENTERING FIELD...";
        public const string ButtonStop = "STOP SPACETIME COMPRESSION";
        public const string ButtonCancel = "STOP CASIMIR GENERATION";
        public const string ButtonExit = "EXIT";
        public const string ToggleBackwards = "Compress trailing field (travel backwards)";
        public const string SliderSpool = "Spool Time (s)";
        public const string SliderEngageDelay = "Field Entry Delay (s)";
        public const string SliderDuration = "Compression Field Duration (s)";
        public const string SliderSpring = "Acceleration Stiffness";
        public const string SliderLaunch = "Launch Strength";
        public const string SliderEndSpeed = "Exit Speed (fraction of cruise)";
        public const string ToggleAutoEnd = "End compression automatically (duration or dropout)";
        public const string ToggleAutoEngage = "Enter field automatically (on engine thrust)";
        public const string LabelInterplanetaryFixed = "Interplanetary Speed (c): {0:G3} (fixed)";
        public const string SliderInterstellar = "Interstellar Speed (c)";
        public const string SliderDropout = "Dropout:SOI Ratio";
        public const string ToggleInterstellar = "Use Interstellar Speed";
        public const string ToggleTargetLock = "Lock compression heading onto selected target";
        public const string SliderAlign = "Target Alignment Tolerance (deg)";

        public static string DirLong(bool backwards) { return backwards ? "AFT" : "FORE"; }             // buttons
        public static string DirShort(bool backwards) { return backwards ? "BACKWARD" : "FORWARD"; }    // messages and log
        public static string DirHud(bool backwards) { return backwards ? "BCK" : "FWD"; }               // on-screen readout

        public static string PrimaryButton(CrunchPhase phase, bool backwards)
        {
            switch (phase)
            {
                case CrunchPhase.Spooling: return ButtonSpooling;
                case CrunchPhase.Ready: return "ENGAGE COMPRESSION (" + DirLong(backwards) + ")";
                case CrunchPhase.Engaging: return ButtonEngaging;
                case CrunchPhase.Crunching: return ButtonStop;
                default: return "START CASIMIR GENERATION (" + DirLong(backwards) + ")";
            }
        }

        public static string DistanceInfo(double seconds, double tierC)
        {
            double meters = tierC * CrunchMath.C * seconds;
            return string.Format("A {0:F1} s compression at {1:G3}c covers {2:G4} Gm ({3:G3} AU, {4:G3} ly)",
                seconds, tierC, meters / 1e9, meters / 1.495978707e11, meters / 9.4607304725808e15);
        }

        // On-Screen Readout
        public const string StatusIdle = "IDLE";
        public static string StatusIdleBecause(string reason) { return "IDLE (" + reason + ")"; }

        public static string StatusSpooling(double elapsed, double total)
        {
            return string.Format("SPOOLING | {0:F1} / {1:F1} s", elapsed, total);
        }

        public const string StatusReady = "READY | Gravitic resonance achieved. Waiting for ENGAGE.";
        public const string StatusWaitingAcceleration = "READY | Gravitic resonance achieved. Waiting for engine thrust.";

        public static string StatusEngaging(double remaining)
        {
            return string.Format("ENGINE THRUST DETECTED | Entering field in {0:F1} s", remaining);
        }

        // A non-positive duration means the crunch has no time limit.
        // lockInfo is either empty or starts with " | " (see StatusTarget).
        public static string StatusCrunching(bool backwards, double tierC, double elapsed, double duration,
                                             double gm, double offAxisDeg, double realSpeed, string body, string lockInfo)
        {
            string time = duration > 0
                ? string.Format("{0:F1} / {1:F1} s", elapsed, duration)
                : string.Format("{0:F1} s", elapsed);
            return string.Format(
                "RIDING GRAVITY WAVE {0} at {1:G3}c ({2:N0} km/s) | {3} | {4:F3} Gm traveled\n" +
                "{5:F1} degrees off-axis | orbital velocity {6:F0} m/s relative to {7}{8}",
                DirHud(backwards), tierC, tierC * CrunchMath.C / 1000.0, time, gm, offAxisDeg, realSpeed, body, lockInfo);
        }

        public static string StatusTarget(string name, double gm)
        {
            return string.Format(" | Target: {0} ({1:F3} Gm to go)", name, gm);
        }

        // Pop-Up Messages
        public const string ScreenPrefix = "[CrunchDrive] ";
        public static string MsgSpoolStart(double seconds) { return string.Format("Spooling ({0:F1} s)", seconds); }
        public const string MsgReady = "Gravitic resonance achieved. Ready to engage compression.";
        public static string MsgEngaged(string dir) { return "Compression engaged (" + dir + ")."; }
        public static string MsgRefused(string reason) { return "Compression refused: " + reason; }
        public static string MsgEnded(string reason) { return "Transition ended: " + reason; }
        public static string MsgSpoolCancelled(string reason) { return "Spool cancelled: " + reason; }

        // Reasons
        public const string RefuseTimeWarp = "Time warp is active!";
        public const string RefuseOtherVessels = "Other vessels are loaded nearby!";
        public const string RefuseAtmosphere = "The vessel is inside an atmosphere!";
        public static string RefuseZone(string body) { return "The vessel is inside " + body + "'s dropout zone!"; }
        public static string RefuseNotAligned(double errDeg, double tolDeg)
        {
            return string.Format("The craft is pointed {0:F1} degrees off its target (limit {1:F1})!", errDeg, tolDeg);
        }

        public const string StopByWindow = "Stopped by debug window.";
        public const string StopVesselChanged = "The active vessel changed.";
        public const string StopTimeWarp = "Time warp was engaged.";
        public const string StopOtherVessel = "Another vessel entered physics range.";
        public const string StopDuration = "Exited compression field.";
        public static string StopDropout(string body) { return "The vessel entered " + body + "'s dropout zone!"; }
    }
}