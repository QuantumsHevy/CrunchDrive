namespace CrunchDrive
{
    public static class CrunchTuning
    {
        public const double InterplanetaryTierC = 8.0;          // Fixed interplanetary speed (c).
        public static double InterstellarTierC = 1.0e5;
        public static bool UseInterstellarTier = false;
        public static double DropoutFraction = 0.02;            // Fraction of each body's SOI used as its dropout radius.
        public static double RootBodyDropoutRadii = 3.0;        // Multiplier for Kerbol's dropout radius.
        public static double SpoolSeconds = 5.0;
        public static double EngageDelaySeconds = 1.0;          // Delay between engine thrust and the crunch starting.
        public static double CrunchSeconds = 10.0;              // Duration limit when AutoEnd is on.
        public static double SpringStiffness = 7.5;             // Spring rate (peak speed comes at duration / stiffness).
        public static double SpringLaunch = 0.0;                // 0 = critically damped spring, 1 = instant-start decay.
        public static double EndSpeedFraction = 0.15;           // Resting speed near the end, as a fraction of cruise speed.
        public static bool AutoEnd = true;
        public static bool AutoEngage = true;                   // Engage after spooling once engine thrust is detected.
        public static bool Backwards = false;
        public static bool UseTargetLock = true;                // Compress toward the selected target instead of the nose.
        public static double TargetAlignToleranceDeg = 15.0;    // Max angle between the craft's axis and the target heading.
    }
}