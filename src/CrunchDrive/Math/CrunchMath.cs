using System;

namespace CrunchDrive.Core
{
    public static class CrunchMath
    {
        public const double C = 299792458.0;

        public static double DisplacementPerTick(double apparentSpeedC, double dt)
            => apparentSpeedC * C * dt;

        // Fraction of the distance still to cover at progress p (0..1) for a critically damped
        // spring with stiffness w and launch L (0 = starts at rest, 1 = instant-start exponential).
        static double Remaining(double p, double w, double launch)
            => (1.0 + w * (1.0 - launch) * p) * Math.Exp(-w * p);

        static double ClampEnd(double endSpeed) => Math.Max(0.0, Math.Min(0.95, endSpeed));

        // Critically damped spring displacement for one tick, blended with constant speed so the
        // craft never slows below endSpeed (a fraction of the nominal speed). Total distance over
        // the full duration equals constant speed (apparentSpeedC * C * duration).
        public static double SpringDisplacementPerTick(double apparentSpeedC, double elapsed, double dt,
                                                       double duration, double stiffness, double launch,
                                                       double endSpeed = 0.0)
        {
            double r = ClampEnd(endSpeed);
            double start = Math.Max(0.0, Math.Min(duration, elapsed));
            double end = Math.Max(start, Math.Min(duration, elapsed + dt));
            double norm = 1.0 - Remaining(1.0, stiffness, launch);
            double spring = (Remaining(start / duration, stiffness, launch)
                           - Remaining(end / duration, stiffness, launch)) / norm;
            double linear = (end - start) / duration;
            double covered = (1.0 - r) * spring + r * linear;
            return apparentSpeedC * C * duration * covered;
        }

        // Instantaneous speed as a multiple of the nominal tier speed, at progress p (0..1).
        public static double SpringSpeedFactor(double progress, double stiffness, double launch,
                                               double endSpeed = 0.0)
        {
            double r = ClampEnd(endSpeed);
            double norm = 1.0 - Remaining(1.0, stiffness, launch);
            double spring = Math.Exp(-stiffness * progress) * stiffness
                          * (launch + stiffness * (1.0 - launch) * progress) / norm;
            return (1.0 - r) * spring + r;
        }

        public static double SecondsForDistance(double meters, double apparentSpeedC)
            => meters / (apparentSpeedC * C);

        // Fraction (0..1) along p0->p1 where the segment first enters the sphere, or null.
        public static double? SweepEntry(Vec3d p0, Vec3d p1, Vec3d center, double radius)
        {
            Vec3d d = p1 - p0, m = p0 - center;
            double c = m.SqrMagnitude - radius * radius;
            if (c <= 0) return 0.0;                         // already inside
            double a = d.SqrMagnitude;
            if (a == 0) return null;
            double b = 2.0 * Vec3d.Dot(d, m);
            double disc = b * b - 4 * a * c;
            if (disc < 0) return null;
            double t = (-b - Math.Sqrt(disc)) / (2 * a);
            return (t >= 0 && t <= 1) ? t : (double?)null;
        }
    }
}