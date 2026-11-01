using CrunchDrive.Core;

namespace CrunchDrive
{
    // Keep any {0} or {1:F1}-style placeholders in the same count and order. Status strings may contain "\n" as line breaks.
    public static class CrunchText
    {
        // Debug Logs
        public const string LogBootstrapPassed = "[CrunchDrive] Kerbal Space Program launched successfully with a CrunchDrive build.";
        public static string LogFlightSceneEntered(string vessel)
        {
            return string.Format("[CrunchDrive] {0} has entered the flight scene.", vessel);
        }
        public const string LogNoGlowMaterial = "[CrunchDrive] No material on the part has _EmissiveColor. Emissive glow will not change.";
        public static string LogGlowMaterials(int materials, int renderers)
        {
            return string.Format("[CrunchDrive] Emissive glow drives {0} material(s) out of {1} renderer(s) on the part.", materials, renderers);
        }
        public const string LogNoTubeShader = "[CrunchDrive] No particle shader was found, some effects will not be drawn.";
        public const string LogTubeAdditive = "[CrunchDrive] Detected shader is additive. Parts may show through effects.";
        public static string LogTubeMeasured(double radius, double forward, double backward, double craftLength)
        {
            return string.Format("[CrunchDrive] Measured effects: radius {0:F1} m, {1:F1} m fore and {2:F1} m aft of the ring (craft length {3:F1} m).", radius, forward, backward, craftLength);
        }
        public static string LogTubeSources(int renderers, int parts, string forwardPart, double forwardAlong, double forwardRadial,
                                            string backwardPart, double backwardAlong, double backwardRadial)
        {
            return string.Format("[CrunchDrive] Effects measured from {0} mesh renderers on {1} parts. Forward end set by {2} ({3:F1} m along, {4:F1} m from the axis); backward end set by {5} ({6:F1} m along, {7:F1} m from the axis).",
                renderers, parts, forwardPart, forwardAlong, forwardRadial, backwardPart, backwardAlong, backwardRadial);
        }
        public static string LogTubePoking(int corners)
        {
            return "[CrunchDrive] " + corners + " corners of the craft are wider than what the effects conceal. Some parts will stick out.";
        }

        // Part (right-click menu and action groups)
        public const string GroupTitle = "Crunch Drive";
        public const string GroupTargetTitle = "Crunch Drive Target";
        public const string GroupSettingsTitle = "Crunch Drive Settings";
        public const string ModuleTitle = "Crunch Drive";
        public const string FieldStatus = "Status";
        public const string FieldCoefficient = "Compression Coefficient";
        public const string FieldSpool = "Casimir State";
        public const string FieldProgress = "Field Status";
        public const string FieldSpeed = "Speed";
        public const string FieldTime = "Field Time";
        public const string FieldTraveled = "Traveled";
        public const string FieldOffAxis = "Off-axis";
        public const string FieldOrbital = "Orbital Velocity";
        public const string FieldLeg = "Maneuver Leg";
        public const string FieldTarget = "Target";
        public const string FieldDistance = "Distance";
        public const string FieldRange = "Fixed-time Range";
        public const string FieldDraw = "Spool Power Draw";
        public const string FieldDropoutAltitude = "Dropout Altitude";
        public const string FieldDropoutNearest = "Nearest Dropout";
        public const string FieldMode = "Travel Mode";
        public const string ToggleOn = "ON";
        public const string ToggleOff = "OFF";
        public const string StatusStandby = "STANDBY";
        public const string ActionPrimary = "Crunch Field (START / ENGAGE / COLLAPSE)";
        public const string ModuleInfo = "Generates a Casimir-compressed gravity well in front of the craft. Range and compression depend on power level and target geometry.";
        public static string ButtonDirection(bool backwards) { return "Field Direction: " + DirLong(backwards); }

        public static string FieldCoefficientValue(double tierC) { return string.Format("{0:G3}c", tierC); }
        public static string FieldSpoolValue(double elapsed, double tierC)
        {
            return string.Format("{0:F1} s ({1:G3}c)", elapsed, tierC);
        }
        public static string FieldProgressValue(double elapsed, double duration) { return string.Format("{0:F0}%", duration > 0.0 ? 100.0 * elapsed / duration : 0.0); }
        public static string FieldSpeedValue(double tierC) { return string.Format("{0:G3}c ({1:N0} km/s)", tierC, tierC * CrunchMath.C / 1000.0); }
        public static string FieldOffAxisValue(double degrees) { return string.Format("{0:F1} degrees", degrees); }
        public static string FieldOrbitalValue(double metersPerSecond, string body) { return string.Format("{0:F0} m/s relative to {1}", metersPerSecond, body); }
        public static string FieldLegValue(int leg, bool backwards) { return string.Format("{0} of 2 ({1})", leg, DirHud(backwards)); }
        public static string FieldDropoutAltitudeValue(double meters, string body) { return FormatDistance(meters) + " above " + body; }
        public static string FieldDropoutNearestValue(string body, double edgeMeters)
        {
            if (edgeMeters < 0.0) return "Inside " + body + "'s dropout zone, " + FormatDistance(-edgeMeters) + " from the edge.";
            return body + ": " + FormatDistance(edgeMeters) + " to the edge.";
        }

        public static string FieldTimeValue(double elapsed, double duration) { return FormatDuration(elapsed) + " / " + FormatDuration(duration); }
        public static string FieldRangeValue(double seconds, double tierC) { return FormatDistance(tierC * CrunchMath.C * seconds); }

        public static string FieldDrawValue(double ecPerSecond) { return string.Format("{0:N0} EC/s", ecPerSecond); }

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
        public const string LabelTier = "Compression Coefficient: {0:G3}c";

        public static readonly string[] ModeNames = { "Full-distance", "Half-distance", "Fixed-time" };

        // Settings (part menu)
        public const string ToggleCaptureAssist = "Use capture assistance";
        public const string ToggleSasRetrograde = "Use capture facilitation";
        public const string ToggleTurnController = "Use MechJeb2-derived attitude controller";
        public const string ToggleCrunchWarp = "Allow time warp while spooling and during long crunches (experimental)";
        public const string ToggleTwoLeg = "Use automatic approach";

        public static string DirLong(bool backwards) { return backwards ? "AFT" : "FORE"; }         // buttons
        public static string DirShort(bool backwards) { return backwards ? "BCK" : "FWD"; }         // messages and log
        public static string DirHud(bool backwards) { return backwards ? "BACKWARD" : "FORWARD"; }  // status readout

        public static string PrimaryButton(CrunchPhase phase, bool backwards, bool turning = false, bool canEngage = false)
        {
            if (phase == CrunchPhase.Crunching && turning) return ButtonAbortApproach;
            switch (phase)
            {
                case CrunchPhase.Spooling: return canEngage ? "ENGAGE COMPRESSION (" + DirLong(backwards) + ")" : ButtonSpooling;
                case CrunchPhase.Ready: return "ENGAGE COMPRESSION (" + DirLong(backwards) + ")";
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
            if (s < 120.0) return string.Format("{0:F1} s", s);
            if (s < 7200.0) return string.Format("{0:F1} min", s / 60.0);
            if (s < 172800.0) return string.Format("{0:F1} h", s / 3600.0);
            return string.Format("{0:F1} d", s / 86400.0);
        }

        // Status Readout
        public const string StatusIdle = "IDLE";
        public static string StatusIdleBecause(string reason) { return "IDLE (" + reason + ")"; }

        public static string StatusSpooling(double elapsed, double tierC, double ecPerSecond, bool canEngage)
        {
            return string.Format("SPOOLING\nCasimir blades spinning up.\n{0:F1} s ({1:G3}c)\nDraw: {2:N0} EC/s.\n", elapsed, tierC, ecPerSecond)
                 + (canEngage ? "Engage when the coefficient is sufficient.\n" : "")
                 + "Throttle and translation are locked out.";
        }

        public const string StatusReady = "READY\nNegative energy threshold reached.\nWaiting for engagement.";

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
            return string.Format("TURNING\nField inactive.\nTrailing region offset: {1:F0}° from {0}.", target, offDeg);
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
            return string.Format("Spool started ({0:F1} s).", seconds);
        }
        public const string MsgSpoolStartOpen = "Spool started.";
        public static string MsgSpoolDry(double tierC)
        {
            return string.Format("Electric charge depleted. Casimir blades stopped accelerating at {0:G3}c.", tierC);
        }
        public static string MsgSpoolFull(double tierC)
        {
            return string.Format("Casimir blades at full speed ({0:G3}c).", tierC);
        }
        public const string MsgReady = "Negative energy threshold reached. Ready for engagement.";
        public static string MsgEngaged(string dir) { return "Compression field active (" + dir + ")."; }
        public static string MsgRefused(string reason) { return "Compression refused: " + reason; }
        public static string MsgEnded(string reason) { return "Compression ended: " + reason + " Engine controls restored."; }
        public static string MsgSpoolCancelled(string reason) { return "Spool cancelled: " + reason + " Engine controls restored."; }
        public static string MsgCaptureAssist(double fromSpeed, double toSpeed, string body)
        {
            return string.Format("Capture assistance reduced orbital velocity from {0:N0} to {1:N0} m/s relative to {2}.", fromSpeed, toSpeed, body);
        }
        public static string MsgIntercept(double fromSpeed, double toSpeed, string target)
        {
            return string.Format("Capture assistance set an intercept with {2}. Relative velocity: {0:N0} to {1:N0} m/s.", fromSpeed, toSpeed, target);
        }
        public const string MsgTurnControllerFallback = "Attitude controller found no steering torque. Attempting SAS.";
        public const string MsgWarpDropped = "Time warp released.";
        public const string MsgFirstLegDone = "Halfway point reached. Compression field stopped. Preparing trailing crunch.";
        public const string MsgSecondLeg = "Trailing region aligned with target. Second compression field active.";

        // Reasons
        public const string RefuseNoDrive = "No crunch drive installed.";
        public const string RefuseTimeWarp = "Time warp active.";
        public const string RefuseOtherVessels = "Other craft loaded nearby.";
        public const string RefuseAtmosphere = "Craft inside an atmosphere.";
        public const string RefuseEnginesActive = "Reaction engines active.";
        public static string RefuseZone(string body) { return "Craft inside " + body + "'s dropout zone."; }
        public static string RefuseNotAligned(double errDeg, double tolDeg)
        {
            return string.Format("Craft pointed {0:F1} degrees off target (limit {1:F1}).", errDeg, tolDeg);
        }
        public const string RefuseNoTarget = "A target is required for this travel mode.";
        public const string RefuseTooClose = "Target is too close for field generation.";

        public const string StopByPlayer = "Stopped by player.";
        public const string StopDriveLost = "Crunch drive lost.";
        public const string StopVesselChanged = "Active craft changed.";
        public const string StopTimeWarp = "Time warp engaged.";
        public const string StopOtherVessel = "Another craft entered physics range.";
        public const string StopEngineThrust = "Engine vibrations unbalanced the Casimir blades.";
        public const string StopTargetLost = "Target lost.";
        public const string StopDuration = "Field collapsed naturally.";
        public static string StopNoCharge(double drawn, double rate)
        {
            return string.Format("Electric charge was depleted before Casimir blades reached operating speed. Drawn {0:N0} EC ({1:N0} EC/s).", drawn, rate);
        }
        public static string StopNearVessel(string vessel) { return "Crunch ended near " + vessel + "."; }
        public static string StopDropout(string body) { return "Crunch ended near " + body + "'s dropout zone."; }
    }
}