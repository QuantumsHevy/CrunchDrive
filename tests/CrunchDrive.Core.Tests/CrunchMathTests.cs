using System;
using CrunchDrive.Core;
using Xunit;

public class CrunchMathTests
{
    [Fact]
    public void Duna_half_trip_at_1c_is_about_17_seconds()
        => Assert.InRange(CrunchMath.SecondsForDistance(5e9, 1.0), 16, 18);

    [Fact]
    public void Fast_segment_hits_sphere_between_ticks()
    {
        var hit = CrunchMath.SweepEntry(new Vec3d(-1e7, 0, 0), new Vec3d(1e7, 0, 0), new Vec3d(0, 0, 0), 1e6);
        Assert.NotNull(hit);
        Assert.InRange(hit.Value, 0.44, 0.46);
    }

    [Fact]
    public void Segment_missing_sphere_returns_null()
        => Assert.Null(CrunchMath.SweepEntry(new Vec3d(-1e7, 5e6, 0), new Vec3d(1e7, 5e6, 0), new Vec3d(0, 0, 0), 1e6));

    // ---- Spring curve ----

    // Sums the spring displacement over a whole crunch in 0.02 s ticks, with a trimmed final tick.
    static double SpringTotal(double tierC, double duration, double stiffness, double launch,
                              out bool allNonNegative, double endSpeed = 0.0)
    {
        const double dt = 0.02;
        double t = 0, total = 0;
        allNonNegative = true;
        while (t < duration - 1e-12)
        {
            double step = Math.Min(dt, duration - t);
            double d = CrunchMath.SpringDisplacementPerTick(tierC, t, step, duration, stiffness, launch, endSpeed);
            if (d < -1e-6) allNonNegative = false;
            total += d;
            t += step;
        }
        return total;
    }

    [Theory]
    [InlineData(2.0, 0.0)]
    [InlineData(8.0, 0.0)]
    [InlineData(10.0, 0.0)]
    [InlineData(5.0, 0.5)]
    [InlineData(20.0, 1.0)]
    public void Spring_covers_same_total_distance_as_constant_speed(double stiffness, double launch)
    {
        double expected = 4.0 * CrunchMath.C * 10.0;
        double total = SpringTotal(4.0, 10.0, stiffness, launch, out bool nonNegative);
        Assert.InRange(total / expected, 0.999999, 1.000001);
        Assert.True(nonNegative);
    }

    [Theory]
    [InlineData(7.5, 0.0, 0.15)]
    [InlineData(8.0, 0.0, 0.3)]
    [InlineData(5.0, 0.5, 0.2)]
    public void Spring_with_end_speed_still_covers_same_total_distance(double stiffness, double launch, double endSpeed)
    {
        double expected = 4.0 * CrunchMath.C * 10.0;
        double total = SpringTotal(4.0, 10.0, stiffness, launch, out bool nonNegative, endSpeed);
        Assert.InRange(total / expected, 0.999999, 1.000001);
        Assert.True(nonNegative);
    }

    [Fact]
    public void Pure_spring_starts_at_rest()
        => Assert.Equal(0.0, CrunchMath.SpringSpeedFactor(0.0, 10.0, 0.0), 9);

    [Fact]
    public void Launch_adds_nonzero_opening_speed()
        => Assert.True(CrunchMath.SpringSpeedFactor(0.0, 8.0, 0.3) > 1.0);

    [Fact]
    public void Pure_spring_peaks_at_one_over_stiffness_and_then_decays()
    {
        double w = 10.0;
        double peak = CrunchMath.SpringSpeedFactor(1.0 / w, w, 0.0);
        Assert.True(peak > CrunchMath.SpringSpeedFactor(0.5 / w, w, 0.0));
        Assert.True(peak > CrunchMath.SpringSpeedFactor(2.0 / w, w, 0.0));
        Assert.InRange(peak, 3.5, 3.8);   // w / e
    }

    [Fact]
    public void Spring_peak_factor_is_about_stiffness_over_e()
        => Assert.InRange(CrunchMath.SpringPeakFactor(10.0, 0.0), 3.67, 3.69);

    [Fact]
    public void A_250c_peak_cap_limits_the_average_to_about_68c()
        => Assert.InRange(250.0 / CrunchMath.SpringPeakFactor(10.0, 0.0), 67.8, 68.0);

    [Fact]
    public void End_speed_sets_the_resting_speed_at_the_end()
        => Assert.InRange(CrunchMath.SpringSpeedFactor(1.0, 8.0, 0.0, 0.2), 0.2, 0.23);

    [Fact]
    public void Speed_never_drops_below_end_speed()
    {
        for (int i = 0; i <= 100; i++)
            Assert.True(CrunchMath.SpringSpeedFactor(i / 100.0, 7.5, 0.0, 0.15) >= 0.15 - 1e-9);
    }

    [Fact]
    public void Speed_factor_matches_per_tick_displacement()
    {
        // Average speed over a short tick should equal the instantaneous factor at its midpoint.
        double w = 8.0, launch = 0.3, duration = 10.0, dt = 0.001, t = 3.0, endSpeed = 0.2;
        double perTick = CrunchMath.SpringDisplacementPerTick(1.0, t, dt, duration, w, launch, endSpeed);
        double factor = perTick / (CrunchMath.C * dt);
        double expected = CrunchMath.SpringSpeedFactor((t + dt / 2) / duration, w, launch, endSpeed);
        Assert.InRange(factor / expected, 0.999, 1.001);
    }

    [Fact]
    public void Spring_displacement_is_zero_after_the_duration()
        => Assert.Equal(0.0, CrunchMath.SpringDisplacementPerTick(4.0, 10.0, 0.02, 10.0, 8.0, 0.0, 0.15), 6);

    // ---- Trapezoid curve ----

    static double TrapezoidTotal(double averageC, double duration, double ramp, out bool allNonNegative)
    {
        const double dt = 0.02;
        double t = 0, total = 0;
        allNonNegative = true;
        while (t < duration - 1e-12)
        {
            double step = Math.Min(dt, duration - t);
            double d = CrunchMath.TrapezoidDisplacementPerTick(averageC, t, step, duration, ramp);
            if (d < -1e-6) allNonNegative = false;
            total += d;
            t += step;
        }
        return total;
    }

    [Theory]
    [InlineData(0.001)]
    [InlineData(0.05)]
    [InlineData(0.1)]
    [InlineData(0.25)]
    public void Trapezoid_covers_same_total_distance_as_constant_speed(double ramp)
    {
        double expected = 225.0 * CrunchMath.C * 100.0;
        double total = TrapezoidTotal(225.0, 100.0, ramp, out bool nonNegative);
        Assert.InRange(total / expected, 0.999999, 1.000001);
        Assert.True(nonNegative);
    }

    [Fact]
    public void Trapezoid_starts_and_ends_at_rest()
    {
        Assert.Equal(0.0, CrunchMath.TrapezoidSpeedFactor(0.0, 0.1), 9);
        Assert.Equal(0.0, CrunchMath.TrapezoidSpeedFactor(1.0, 0.1), 9);
    }

    [Fact]
    public void Trapezoid_cruise_speed_is_the_peak()
        => Assert.Equal(1.0 / 0.9, CrunchMath.TrapezoidSpeedFactor(0.5, 0.1), 9);

    [Fact]
    public void Trapezoid_peak_factor_matches_cruise_speed()
        => Assert.Equal(CrunchMath.TrapezoidPeakFactor(0.1), CrunchMath.TrapezoidSpeedFactor(0.5, 0.1), 9);

    [Fact]
    public void A_250c_peak_cap_gives_a_225c_trapezoid_average()
        => Assert.Equal(225.0, 250.0 / CrunchMath.TrapezoidPeakFactor(0.1), 6);

    [Fact]
    public void Trapezoid_ramps_rise_and_fall_smoothly()
    {
        double previous = 0.0;
        for (int i = 0; i <= 100; i++)               // Ramp up over the first 10% of the crunch.
        {
            double f = CrunchMath.TrapezoidSpeedFactor(i / 1000.0, 0.1);
            Assert.True(f >= previous - 1e-9);
            previous = f;
        }
        previous = CrunchMath.TrapezoidSpeedFactor(0.9, 0.1);
        for (int i = 1; i <= 100; i++)               // Ramp down over the last 10%.
        {
            double f = CrunchMath.TrapezoidSpeedFactor(0.9 + i / 1000.0, 0.1);
            Assert.True(f <= previous + 1e-9);
            previous = f;
        }
    }

    [Fact]
    public void Trapezoid_spends_most_of_the_crunch_near_peak_speed()
    {
        double peak = CrunchMath.TrapezoidPeakFactor(0.1);
        int near = 0;
        for (int i = 0; i < 1000; i++)
            if (CrunchMath.TrapezoidSpeedFactor((i + 0.5) / 1000.0, 0.1) >= 0.99 * peak) near++;
        Assert.True(near >= 800);
    }

    [Fact]
    public void Trapezoid_speed_factor_matches_per_tick_displacement()
    {
        double ramp = 0.1, duration = 10.0, dt = 0.001, t = 0.5;
        double perTick = CrunchMath.TrapezoidDisplacementPerTick(1.0, t, dt, duration, ramp);
        double factor = perTick / (CrunchMath.C * dt);
        double expected = CrunchMath.TrapezoidSpeedFactor((t + dt / 2) / duration, ramp);
        Assert.InRange(factor / expected, 0.999, 1.001);
    }

    [Fact]
    public void Trapezoid_displacement_is_zero_after_the_duration()
        => Assert.Equal(0.0, CrunchMath.TrapezoidDisplacementPerTick(225.0, 10.0, 0.02, 10.0, 0.1), 6);

    // ---- Arrival ----

    [Fact]
    public void Kerbin_escape_speed_at_its_dropout_edge_is_about_2_km_per_second()
        => Assert.InRange(CrunchMath.EscapeSpeed(3.5316e12, 1.68e6), 2040.0, 2060.0);

    [Fact]
    public void Capture_assistance_limits_fast_arrivals_to_a_fraction_of_escape_speed()
    {
        double escape = CrunchMath.EscapeSpeed(3.5316e12, 1.68e6);
        Assert.Equal(0.9 * escape, CrunchMath.CappedArrivalSpeed(20000.0, 3.5316e12, 1.68e6, 0.9), 6);
    }

    [Fact]
    public void Capture_assistance_leaves_slow_arrivals_alone()
        => Assert.Equal(500.0, CrunchMath.CappedArrivalSpeed(500.0, 3.5316e12, 1.68e6, 0.9), 9);

    // ---- Capture assistance ----

    const double Mu = 3.5316e12;   // Kerbin
    const double R = 1.68e6;       // Its dropout edge

    [Fact]
    public void Circular_orbit_has_its_periapsis_at_the_current_radius()
    {
        double v = Math.Sqrt(Mu / R);
        // Exactly circular makes the eccentricity term pure rounding noise, so compare to a relative tolerance.
        Assert.InRange(CrunchMath.PeriapsisRadius(Mu, R, v, R * v) / R, 1.0 - 1e-6, 1.0 + 1e-6);
    }

    [Fact]
    public void Sideways_and_faster_than_circular_puts_periapsis_at_the_current_radius()
    {
        double v = 1.2 * Math.Sqrt(Mu / R);
        Assert.Equal(R, CrunchMath.PeriapsisRadius(Mu, R, v, R * v), 3);
    }

    [Fact]
    public void Sideways_and_slower_than_circular_puts_periapsis_below_the_current_radius()
    {
        double v = 0.8 * Math.Sqrt(Mu / R);
        double nu = v * v * R / Mu;
        Assert.Equal(R * nu / (2.0 - nu), CrunchMath.PeriapsisRadius(Mu, R, v, R * v), 3);
    }

    [Fact]
    public void A_dead_radial_plunge_has_a_periapsis_at_the_center()
        => Assert.Equal(0.0, CrunchMath.PeriapsisRadius(Mu, R, 5000.0, 0.0), 6);

    [Fact]
    public void Min_speed_for_periapsis_gives_exactly_that_periapsis()
    {
        double rp = 0.5 * R;
        double v = CrunchMath.MinSpeedForPeriapsis(Mu, R, rp);
        Assert.InRange(v, 1.0, Math.Sqrt(Mu / R));
        Assert.Equal(rp, CrunchMath.PeriapsisRadius(Mu, R, v, R * v), 3);
    }

    [Fact]
    public void Min_speed_for_a_periapsis_at_or_above_the_radius_is_zero()
    {
        Assert.Equal(0.0, CrunchMath.MinSpeedForPeriapsis(Mu, R, R), 9);
        Assert.Equal(0.0, CrunchMath.MinSpeedForPeriapsis(Mu, R, 2.0 * R), 9);
    }

    // ---- Dropout zones ----

    [Fact]
    public void Moho_dropout_zone_sits_above_the_surface()
        => Assert.Equal(350000.0, CrunchMath.DropoutRadius(9646663.0, 250000.0, 0.0, 0.02, 100000.0, 3.0), 3);

    [Fact]
    public void Laythe_dropout_zone_clears_the_atmosphere()
        => Assert.Equal(650000.0, CrunchMath.DropoutRadius(3723645.0, 500000.0, 50000.0, 0.02, 100000.0, 3.0), 3);

    [Fact]
    public void Large_soi_uses_the_soi_fraction()
        => Assert.InRange(CrunchMath.DropoutRadius(84159286.0, 600000.0, 70000.0, 0.02, 100000.0, 3.0), 1.68e6, 1.69e6);

    [Fact]
    public void Dropout_zone_never_exceeds_the_soi()
        => Assert.Equal(50000.0, CrunchMath.DropoutRadius(50000.0, 10000.0, 0.0, 0.02, 100000.0, 3.0), 3);

    [Fact]
    public void Root_body_uses_a_multiple_of_its_radius()
        => Assert.Equal(3.0 * 261600000.0, CrunchMath.DropoutRadius(double.PositiveInfinity, 261600000.0, 0.0, 0.02, 100000.0, 3.0), 3);

    [Fact]
    public void Trapezoid_with_a_tiny_ramp_reaches_full_speed_almost_at_once()
    {
        // A 4 s ramp on a 3500 s crunch.
        double ramp = 4.0 / 3500.0;
        Assert.Equal(CrunchMath.TrapezoidPeakFactor(ramp), CrunchMath.TrapezoidSpeedFactor(5.0 / 3500.0, ramp), 9);
        Assert.InRange(CrunchMath.TrapezoidPeakFactor(ramp), 1.0, 1.002);
    }

    // ---- Attitude control and intercepts ----

    [Fact]
    public void Turn_rate_limit_follows_the_square_root_law_far_from_the_target()
        => Assert.Equal(Math.Sqrt(2.0 * 0.5 * 0.2 * 3.0), CrunchMath.TurnRateLimit(0.2, 3.0, 0.5, 10.0, 100.0), 9);

    [Fact]
    public void Turn_rate_limit_is_linear_close_to_the_target()
        => Assert.Equal(0.01, CrunchMath.TurnRateLimit(0.2, 0.01, 0.5, 10.0, 1.0), 9);

    [Fact]
    public void Turn_rate_limit_never_exceeds_the_maximum_rate()
        => Assert.Equal(0.3, CrunchMath.TurnRateLimit(50.0, 3.0, 0.5, 0.3, 100.0), 9);

    [Fact]
    public void Turn_rate_limit_is_zero_with_no_error_or_no_authority()
    {
        Assert.Equal(0.0, CrunchMath.TurnRateLimit(0.2, 0.0, 0.5, 1.0, 1.0), 9);
        Assert.Equal(0.0, CrunchMath.TurnRateLimit(0.0, 1.0, 0.5, 1.0, 1.0), 9);
    }

    [Fact]
    public void Intercept_speed_is_the_approach_speed_when_far()
        => Assert.Equal(200.0, CrunchMath.InterceptSpeed(75000.0, 200.0, 30.0), 9);

    [Fact]
    public void Intercept_speed_slows_down_when_close()
        => Assert.Equal(10.0, CrunchMath.InterceptSpeed(300.0, 200.0, 30.0), 9);
}