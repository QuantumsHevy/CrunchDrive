namespace CrunchDrive
{
    public enum TravelMode { FullDistance, HalfDistance, FixedDuration }

    public static class CrunchTuning
    {
        // ---- Speed ----
        public const double InterplanetaryTierC = 25.0;         // Average speed (c) for targets inside the system.
        public const double InterstellarTierC = 250.0;          // Requested average (c) for far targets; clamped by the cap below.
        public const double MaxSpeedC = 250.0;                  // Hard cap on the instantaneous PEAK speed (c).
        public const double InterstellarEelooFactor = 3.0;      // Far = target beyond this x the distance from the root star to Eeloo.
        public const double InterstellarRampSeconds = 4.0;      // Interstellar ramp-up and ramp-down, each this many seconds (at most half the crunch).

        // ---- Spool and safety ----
        public const double SpoolSeconds = 2.5;
        public const double EngineGraceSeconds = 0.25;          // Engine thrust tolerated this long before the drive aborts.

        // ---- Targeting ----
        public const double TargetAlignToleranceDeg = 10.0;     // How far off the target the craft may be when a crunch starts.
        public const double TurnAlignToleranceDeg = 2.0;        // Before the second leg: how far the tail may be off the target.
        public const double TurnSettleRateDegPerSec = 0.5;      // Before the second leg: the craft must be turning slower than this...
        public const double TurnSettleSeconds = 0.5;            // ...for this long, while aligned.
        public const double MinCrunchSeconds = 0.1;
        public const double VesselStopDistance = 75000.0;       // Stop this far short of a targeted vessel (m).

        // ---- Turn between the legs (attitude controller) ----
        public const double TurnProbeSeconds = 0.2;             // Calibration: length of each test pulse (baseline, pitch, yaw, roll).
        public const double TurnProbeInput = 0.25;              // Calibration: control input used for the test pulses (0..1).
        public const double TurnMinAuthority = 1e-4;            // Calibration: below this rad/s^2 per full input, an axis counts as having no torque.
        public const double TurnMaxRateDegPerSec = 30.0;        // Never turn faster than this.
        public const double TurnBrakingMargin = 0.5;            // Plan the braking with this fraction of the craft's angular acceleration.
        public const double TurnRateGain = 1.0;                 // Close to the target the turning rate is this x the remaining angle (per second).
        public const double TurnResponseSeconds = 0.5;          // The rate error is corrected over about this long.

        // ---- Dropout zones ----
        public const double DropoutFraction = 0.0125;           // Fraction of each body's SOI.
        public const double MinDropoutAltitude = 100000.0;      // Zones reach at least this far above surface/atmosphere (m).
        public const double RootBodyDropoutRadii = 2.0;         // Multiplier for Kerbol's dropout radius.

        // ---- Arrival ----
        public const double CaptureSpeedFraction = 0.9875;      // Capture assistance slows the craft to at most this x local escape speed.
        public const double CaptureSafeAltitude = 50000.0;      // Capture assistance keeps periapsis at least this far above the surface/atmosphere (m).
        public const double ArrivalSettleSeconds = 0.125;       // Longest wait for KSP to switch the reference body after a crunch.
        public const double ArrivalDelaySeconds = 0.125;       // Wait this long after the switch before SAS is set and capture assistance starts.
        public const double ArrivalAimTimeoutSeconds = 120.0;   // After capture assistance is done, give up waiting for the craft to settle retrograde after this long.
        public const double InterceptApproachSpeed = 200.0;     // Intercept assistance closes on a targeted craft at this speed (m/s)...
        public const double InterceptMinSeconds = 30.0;         // ...slowing down so the remaining distance always takes at least this long.
        public const double InterceptMaxDistance = 1000000.0;   // Farther than this from the target craft (m), the craft is captured by the body instead.

        // ---- Interplanetary speed curve (critically damped spring) ----
        public const double SpringStiffness = 10.0;
        public const double SpringLaunch = 0.0;
        public const double EndSpeedFraction = 0.0;

        // ---- Player controls (move to the part later) ----
        public static TravelMode Mode = TravelMode.FullDistance;    // Single-leg crunches only; a two-leg crunch plans its own distances.
        public static double CrunchSeconds = 10.0;              // Duration in FixedDuration mode (single-leg crunches only).
        public static bool Backwards = false;                   // Orientation of the FIRST leg. The second leg is always backwards.
        public static bool TwoLegManeuver = true;               // Crunch to a planet, moon or craft in two legs: forward halfway, flip, backward the rest.
        public static bool UseTurnController = true;            // Turn between the legs with the attitude controller instead of SAS.
        public static bool AllowInterstellarWarp = true;        // Interstellar crunches may run under on-rails time warp. EXPERIMENTAL.
        public static bool SasRetrograde = true;               // On arrival, set SAS to retrograde (only if SAS is on). The craft is never rotated by force.
        public static bool CaptureAssist = true;                // On arrival, gradually slow/redirect the velocity until the craft is bound and clear of the surface (or on course to a targeted craft).
        public static double CaptureRampSeconds = 4.0;          // Capture assistance spreads its velocity change over this long. 0 = one instant impulse.
    }
}