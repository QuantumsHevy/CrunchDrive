namespace CrunchDrive
{
    public enum TravelMode { FullDistance, HalfDistance, FixedDuration }

    public static class CrunchTuning
    {
        // ---- Speed ----
        public const double MinSpeedC = 2.5;    // Average speed (c) of the shortest spool. Keeps a crunch well above 1c.
        public const double MaxSpeedC = 250.0;  // Average speed (c) of a full spool, and the hard cap on the PEAK speed of any crunch. The average of a short leg is lower: its ramps take a larger share (a very short leg averages half the peak).
        public const double RampSeconds = 7.5;  // Ramp-up and ramp-down duration of every crunch, each capped at half the leg.

        // ---- Spool (the Casimir blades spin up for as long as the player likes; a longer spool compresses the field harder) ----
        public const double SpoolEcPerSecond = 5000.0;  // Charge one spool second costs, and the draw a new part starts at (the player can change the draw). A part can override it with its own spoolEcPerSecond in the cfg. A full spool (MaxSpoolSeconds) costs MaxSpoolSeconds x this, however fast it is drawn. The craft's stored charge is only a buffer: when it can no longer supply the draw, the spool ends.
        public const double SpoolRateMinEc = 1000.0;    // Lowest draw (EC/s) the player can set: a slower spool that is easier on the craft's power.
        public const double SpoolRateMaxEc = 50000.0;   // Highest draw (EC/s) the player can set: spool seconds are bought this much faster, so it needs that much supply.
        public const double SpoolRateStepEc = 1000.0;   // Step of the draw setting (EC/s).
        public const double MinSpoolSeconds = 5.0;      // Shortest spool: the crunch can be engaged after this long, and a spool shorter than this gives MinSpeedC.
        public const double MaxSpoolSeconds = 1800.0;   // A spool this long (at the full draw) gives MaxSpeedC and ends by itself. The speed rises in a straight line from MinSpeedC at MinSpoolSeconds.
        public const double SpoolDrySeconds = 0.125;    // The spool ends once the craft has been unable to supply the full draw for this long. 0 = the first short tick.
        public const double GlowSpoolSeconds = 5;       // The part's emissive glow reaches full this many spool seconds after the spool starts.

        // ---- Time warp ----
        // Time warp is allowed during every spool, including the second one between the legs (if the setting is on).
        public const double WarpMinLegSeconds = 300.0;      // During the crunch itself, on-rails time warp is allowed only for legs at least this long.
        public const double WarpFinalSeconds = 5.0;         // Time warp is back to 1x this many seconds before a crunch leg ends.
        public const double WarpSpoolFinalSeconds = 10.0;   // Time warp is back to 1x this many seconds before a spool ends.
        public const double WarpStepSeconds = 2.5;          // Before that, each warp rate lasts at most about this many real seconds.

        // ---- Sound cues ----
        public const double CueHoldSeconds = 6.4;           // One-shot cues are held on this long, then reset so they can play again. Set it to the length of your longest one-shot clip.
        public const double SpoolPitchRiseSeconds = 2.5;    // The spool loop's pitch rises from SpoolPitchStart to normal over this long after the spool starts.
        public const double SpoolPitchFallSeconds = 1.5;    // When the spool ends without an engage (ready, or cancelled), the spool loop's pitch falls to SpoolPitchFloor over this long, then stops. After an engage it falls over TubeFadeInSeconds instead.
        public const double SpoolPitchStart = 0.5;          // Pitch at the start of the spool.
        public const double SpoolPitchFloor = 0.25;         // Pitch the spool loop falls to as the field forms, before it ends. Must match the low end of the cue's pitch curve in the part's config.

        // ---- Glow ----
        public const double GlowFadeSeconds = 5.0;          // The part's emissive glow fades back to grey over this long after a crunch (or a cancelled spool).

        // ---- Tube of light (the blinding white tube around the craft while a crunch field is up) ----
        public const double TubeRadiusFactor = 0.75;                // Tube radius = the ring's outer radius x this.
        public const double TubeExtraLengthFraction = 0.0;          // was 0.125
        public const int TubeSegments = 24;                         // was 48
        public const double TubeFadeInSeconds = 0.25;               // The tube grows from the ring to its full length, and fades in, over this long when the field forms. The craft waits this long before it starts to move, so the tube is at full length first.
        public const double TubeFadeOutSeconds = 1.25;              // The tube shrinks back to the ring, and fades out, over this long when the field is down.
        public const double TubeLightIntensity = 2.5;               // was 5.0
        public const double TubeLightRangeFactor = 1.25;            // was 1.0
        public const double TubeStretchAmount = 0.025;              // was 0.075
        public const double TubeFlickerHz = 32.0;                   // How fast they waver (roughly flickers per second).
        public const double TubeFlickerAmount = 0.25;               // was 0.125
        public const double TubeMinHalfLengthRingDiameters = 1.0;   // However small the craft, each end of the tube is at least this many ring diameters long (so the whole tube is at least twice that).
        public const double TubeShapeExponent = 2.0;                // Curve of the tube: widest at the ring, closing to a point at each end. 2 = an ellipse; higher = fuller near the ring, sharper at the tips. 1 is a straight cone, and lower counts as 1.
        public const double TubeFitMaxFactor = 1.75;                // was 2.5
        public const int TubeRings = 12;                            // was 24
        public const int TubeHaloShells = 8;                        // was 12
        public const double TubeHaloSpread = 0.5;                   // was 1.125
        public const double TubeHaloStrength = 0.125;               // How bright the glow is right at the tube's edge, 0..1. It falls off smoothly from there to nothing at the outermost layer. (The old per-layer value 0.05 with 5 layers came to about 0.15.)
        public const double TubeHaloFalloff = 0.5;                  // Width of that falloff as a share of the glow's width: smaller = a tight glow that dies quickly, larger = a flatter, wider haze.

        // ---- Flash (a brief, overwhelming glow at the ring, every time the craft enters or leaves the crunch field) ----
        public const double FlashRiseSeconds = 0.075;   // The flash reaches full brightness this fast.
        public const double FlashFadeSeconds = 0.125;   // Then it dies away over this long (fast at first, with a long soft tail).
        public const double FlashSizeFactor = 2.5;      // was 1.25
        public const double FlashGrowth = 2.0;          // was 0.75
        public const double FlashStrength = 1.0;        // Peak opacity, 0..1.
        public const double FlashLightIntensity = 2.5;  // was 15.0

        // ---- Safety ----
        public const double EngineGraceSeconds = 0.25;  // Engine thrust tolerated this long before the drive aborts.

        // ---- Targeting ----
        public const double TargetAlignToleranceDeg = 10.0; // How far off the target the craft may be when a crunch starts.
        public const double TurnAlignToleranceDeg = 2.5;    // Before the second leg: how far the tail may be off the target.
        public const double TurnSettleRateDegPerSec = 0.5;  // Before the second leg, remain below this rate while aligned.
        public const double TurnSettleSeconds = 0.5;        // Required time below the rate threshold.
        public const double MinCrunchSeconds = 0.1;
        public const double VesselStopDistance = 75000.0;   // Stop this far short of a targeted vessel (m).

        // ---- Turn between the legs (attitude controller) ----
        public const double TurnProbeSeconds = 0.125;       // Duration of each calibration stage.
        public const double TurnProbeInput = 0.25;          // Calibration: control input used for the test pulses (0..1).
        public const double TurnMinAuthority = 1e-4;        // Calibration: below this rad/s^2 per full input, an axis counts as having no torque.
        public const double TurnMaxRateDegPerSec = 60.0;    // Never turn faster than this.
        public const double TurnBrakingMargin = 0.5;        // Plan the braking with this fraction of the craft's angular acceleration.
        public const double TurnRateGain = 1.0;             // Close to the target the turning rate is this x the remaining angle (per second).
        public const double TurnResponseSeconds = 0.5;      // The rate error is corrected over about this long.

        // ---- Dropout zones ----
        public const double DropoutFraction = 0.00625;      // Fraction of each body's SOI.
        public const double MinDropoutAltitude = 100000.0;  // Zones reach at least this far above surface/atmosphere (m).
        public const double RootBodyDropoutRadii = 2.0;     // Multiplier for Kerbol's dropout radius.

        // ---- Arrival ----
        public const double CaptureSpeedFraction = 0.9875;      // Capture assistance slows the craft to at most this x local escape speed.
        public const double CaptureSafeAltitude = 50000.0;      // Capture assistance keeps periapsis at least this far above the surface/atmosphere (m).
        public const double ArrivalSettleSeconds = 0.125;       // Longest wait for KSP to switch the reference body after a crunch.
        public const double ArrivalDelaySeconds = 0.125;        // Wait this long after the switch before SAS is set and capture assistance starts.
        public const double ArrivalAimTimeoutSeconds = 30.0;    // After capture assistance is done, give up waiting for the craft to settle retrograde after this long.
        public const double InterceptApproachSpeed = 200.0;     // Target closing speed for craft intercepts (m/s).
        public const double InterceptMinSeconds = 30.0;         // Minimum duration of the final approach.
        public const double InterceptMaxDistance = 1000000.0;   // Farther than this from the target craft (m), the craft is captured by the body instead.

        // ---- Player controls (live values; the primary crunch drive copies its craft settings here, and the debug window sets the last two) ----
        public static TravelMode Mode = TravelMode.FullDistance;    // Single-leg crunches only; a two-leg crunch plans its own distances.
        public static double CrunchSeconds = 10.0;                  // Duration in FixedDuration mode (single-leg crunches only).
        public static bool Backwards = false;                       // Orientation of the FIRST leg. The second leg is always backwards.
        public static bool TwoLegManeuver = true;                   // Crunch to a planet, moon or craft in two legs: forward halfway, flip, backward the rest.
        public static bool UseTurnController = true;                // Turn between the legs with the attitude controller instead of SAS.
        public static bool AllowWarp = true;                        // The spool and long crunches may run under time warp. EXPERIMENTAL.
        public static bool SasRetrograde = true;                    // On arrival, set SAS to retrograde (only if SAS is on). The craft is never rotated by force.
        public static bool CaptureAssist = true;                    // On arrival, gradually slow/redirect the velocity until the craft is bound and clear of the surface (or on course to a targeted craft).
        public static double CaptureRampSeconds = 2.75;             // Duration of the velocity change; 0 applies it instantly.
    }
}