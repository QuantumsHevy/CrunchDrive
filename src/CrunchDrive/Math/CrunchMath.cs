using System;

namespace CrunchDrive.Core
{
    public static class CrunchMath
    {
        public const double C = 299792458.0;

        // ---- Spring curve (interplanetary) ----

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

        // Highest speed factor (multiple of the average tier) the curve ever reaches.
        public static double SpringPeakFactor(double stiffness, double launch, double endSpeed = 0.0)
        {
            double peak = 0.0;
            for (int i = 0; i <= 2000; i++)
                peak = Math.Max(peak, SpringSpeedFactor(i / 2000.0, stiffness, launch, endSpeed));
            return peak;
        }

        // ---- Trapezoid curve (interstellar): ease up, hold peak speed, ease down ----
        // Each ramp lasts rampFraction of the duration and is smoothstep-shaped, so speed starts and ends
        // at zero without corners. With a peak speed of 1, the total distance is (1 - rampFraction).

        static double ClampRamp(double ramp) => Math.Max(1e-6, Math.Min(0.5, ramp));

        static double Smoothstep(double x) => x * x * (3.0 - 2.0 * x);

        // Integral of smoothstep from 0 to x.
        static double SmoothstepArea(double x) => x * x * x - 0.5 * x * x * x * x;

        // Distance covered by progress p (0..1), as a multiple of peak speed x duration.
        static double TrapezoidCumulative(double p, double r)
        {
            p = Math.Max(0.0, Math.Min(1.0, p));
            if (p < r) return r * SmoothstepArea(p / r);
            if (p <= 1.0 - r) return 0.5 * r + (p - r);
            return (1.0 - r) - r * SmoothstepArea((1.0 - p) / r);
        }

        // Peak speed as a multiple of the average tier speed.
        public static double TrapezoidPeakFactor(double rampFraction)
            => 1.0 / (1.0 - ClampRamp(rampFraction));

        // Trapezoid displacement for one tick. Total distance over the full duration equals
        // constant speed (averageSpeedC * C * duration), the same as the spring.
        public static double TrapezoidDisplacementPerTick(double averageSpeedC, double elapsed, double dt,
                                                          double duration, double rampFraction)
        {
            double r = ClampRamp(rampFraction);
            double start = Math.Max(0.0, Math.Min(duration, elapsed));
            double end = Math.Max(start, Math.Min(duration, elapsed + dt));
            double covered = (TrapezoidCumulative(end / duration, r) - TrapezoidCumulative(start / duration, r)) / (1.0 - r);
            return averageSpeedC * C * duration * covered;
        }

        // Instantaneous speed as a multiple of the average tier speed, at progress p (0..1).
        public static double TrapezoidSpeedFactor(double progress, double rampFraction)
        {
            double r = ClampRamp(rampFraction);
            double p = Math.Max(0.0, Math.Min(1.0, progress));
            double unit;
            if (p < r) unit = Smoothstep(p / r);
            else if (p > 1.0 - r) unit = Smoothstep((1.0 - p) / r);
            else unit = 1.0;
            return unit / (1.0 - r);
        }

        // ---- Arrival ----

        // Speed (m/s) needed to escape a body's gravity from the given distance from its center.
        public static double EscapeSpeed(double gravParameter, double radius)
            => Math.Sqrt(2.0 * gravParameter / radius);

        // Capture assistance: an arrival speed limited to a fraction of local escape speed.
        // Slower arrivals are returned unchanged.
        public static double CappedArrivalSpeed(double speed, double gravParameter, double radius, double escapeFraction)
            => Math.Min(speed, escapeFraction * EscapeSpeed(gravParameter, radius));

        // Periapsis radius of the orbit through a point at the given radius, with the given speed and
        // angular momentum (|r x v|). Works for bound and unbound orbits.
        public static double PeriapsisRadius(double gravParameter, double radius, double speed, double angularMomentum)
        {
            double energy = speed * speed * 0.5 - gravParameter / radius;
            double ecc = Math.Sqrt(Math.Max(0.0, 1.0 + 2.0 * energy * angularMomentum * angularMomentum
                                                  / (gravParameter * gravParameter)));
            return angularMomentum * angularMomentum / gravParameter / (1.0 + ecc);
        }

        // Slowest purely sideways speed at the given radius whose orbit still has a periapsis of at least
        // periapsisRadius (the speed of an orbit whose apoapsis is here). Zero if that periapsis is not below this radius.
        public static double MinSpeedForPeriapsis(double gravParameter, double radius, double periapsisRadius)
            => periapsisRadius < radius
                ? Math.Sqrt(2.0 * gravParameter * periapsisRadius / (radius * (radius + periapsisRadius)))
                : 0.0;

        // ---- Dropout, timing, collision ----

        // Radius from a body's center at which a crunch is forced to drop out. Normally a fraction of the
        // SOI, but never below the surface (or top of the atmosphere) plus minAltitude, and never beyond
        // the SOI itself. The root body (infinite SOI) uses a multiple of its radius.
        public static double DropoutRadius(double soi, double bodyRadius, double atmosphereDepth,
                                           double soiFraction, double minAltitude, double rootRadii)
        {
            if (double.IsInfinity(soi) || soi > 1e20) return bodyRadius * rootRadii;
            double byFraction = soi * soiFraction;
            double byFloor = bodyRadius + atmosphereDepth + minAltitude;
            return Math.Min(soi, Math.Max(byFraction, byFloor));
        }

        // ---- Attitude control and intercepts ----

        // Fastest turning rate (rad/s) the craft may have while still `angleRad` away from its target attitude.
        // Square-root law: the rate from which the craft can still brake in time using `margin` of its
        // angular acceleration `alpha` (rad/s^2). Close in it blends into a gentle linear approach (rate = gain * angle)
        // so the controller doesn't chatter, and it never exceeds maxRate.
        public static double TurnRateLimit(double alpha, double angleRad, double margin, double maxRate, double gain)
        {
            if (!(angleRad > 0.0) || !(alpha > 0.0)) return 0.0;
            double braking = Math.Sqrt(2.0 * Math.Max(0.0, margin) * alpha * angleRad);
            double linear = gain * angleRad;
            return Math.Max(0.0, Math.Min(maxRate, Math.Min(braking, linear)));
        }

        // Speed (m/s) a craft should close on a target craft at: the approach speed, but slow enough
        // that the remaining distance takes at least minSeconds.
        public static double InterceptSpeed(double distance, double approachSpeed, double minSeconds)
        {
            if (!(distance > 0.0)) return 0.0;
            return Math.Min(approachSpeed, distance / Math.Max(1e-6, minSeconds));
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