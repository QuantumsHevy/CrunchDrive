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
    [InlineData(8.0, 0.3)]
    [InlineData(5.0, 0.5)]
    [InlineData(20.0, 1.0)]
    public void Spring_covers_same_total_distance_as_constant_speed(double stiffness, double launch)
    {
        double expected = CrunchMath.DisplacementPerTick(4.0, 10.0);
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
        double expected = CrunchMath.DisplacementPerTick(4.0, 10.0);
        double total = SpringTotal(4.0, 10.0, stiffness, launch, out bool nonNegative, endSpeed);
        Assert.InRange(total / expected, 0.999999, 1.000001);
        Assert.True(nonNegative);
    }

    [Fact]
    public void Pure_spring_starts_at_rest()
        => Assert.Equal(0.0, CrunchMath.SpringSpeedFactor(0.0, 8.0, 0.0), 9);

    [Fact]
    public void Launch_adds_nonzero_opening_speed()
        => Assert.True(CrunchMath.SpringSpeedFactor(0.0, 8.0, 0.3) > 1.0);

    [Fact]
    public void Pure_spring_peaks_at_one_over_stiffness_and_then_decays()
    {
        double w = 8.0;
        double peak = CrunchMath.SpringSpeedFactor(1.0 / w, w, 0.0);
        Assert.True(peak > CrunchMath.SpringSpeedFactor(0.5 / w, w, 0.0));
        Assert.True(peak > CrunchMath.SpringSpeedFactor(2.0 / w, w, 0.0));
        Assert.InRange(peak, 2.8, 3.1);   // w / e
    }

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
}