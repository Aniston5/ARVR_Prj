using System;

public static class SoilMoistureProfileChecks
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    public static void Main()
    {
        var data = new SoilMoistureProfile();
        float value;
        Check(!data.TryGet(0, 0, 0, 10, out value), "Empty sensor must not appear valid");
        Check(data.Set(0, 0, 0, 10), "Zero moisture is valid");
        Check(data.TryGet(0, 0, 20, 10, out value) && value == 0, "Boundary freshness");
        Check(!data.TryGet(0, 0, 20.001, 10, out value), "Stale reading must expire");
        Check(!data.TryGet(0, 1, 10, 10, out value), "Other depth must stay empty");
        Check(!data.TryGet(1, 0, 10, 10, out value), "Other plant must stay empty");
        Check(data.Set(3, 2, 100, 10), "Final sensor and 100% must be valid");
        Check(!data.Set(4, 0, 40, 10) && !data.Set(0, 3, 40, 10), "Invalid slot accepted");
        Check(!data.Set(0, 0, float.NaN, 10) && !data.Set(0, 0, float.PositiveInfinity, 10), "Invalid float accepted");
        Check(!data.Set(0, 0, -1, 10) && !data.Set(0, 0, 101, 10), "Out of range accepted");
        Check(data.TryGet(0, 0, 10, 10, out value) && value == 0, "Rejected input changed last value");
        data.Clear();
        Check(!data.TryGet(3, 2, 10, 10, out value), "Reset must clear all sensors");
        for (int p = 0; p < 4; p++)
        {
            Check(SoilMoistureProfile.DemoValue(p, 0, 2) > SoilMoistureProfile.DemoValue(p, 0, 0), "Shallow should rise first");
            Check(SoilMoistureProfile.DemoValue(p, 1, 2) == SoilMoistureProfile.DemoValue(p, 1, 0), "Middle rose too soon");
            Check(SoilMoistureProfile.DemoValue(p, 1, 5) > SoilMoistureProfile.DemoValue(p, 1, 0), "Middle should rise next");
            Check(SoilMoistureProfile.DemoValue(p, 2, 5) == SoilMoistureProfile.DemoValue(p, 2, 0), "Deep rose too soon");
            Check(SoilMoistureProfile.DemoValue(p, 2, 9) > SoilMoistureProfile.DemoValue(p, 2, 0), "Deep should rise last");
            for (int d = 0; d < 3; d++)
                for (int t = 0; t < 500; t++)
                {
                    float v = SoilMoistureProfile.DemoValue(p, d, t);
                    Check(v >= 0 && v <= 100, "Demo outside valid range");
                }
        }
        var samples = new[] {
            new MoistureField.Sample { x = -1, percent = 20, valid = true },
            new MoistureField.Sample { x = 1, percent = 80, valid = true }
        };
        Check(MoistureField.Estimate(samples, -1, 0, 0, out value) && value == 20, "Exact sensor value must be preserved");
        Check(MoistureField.Estimate(samples, 0, 0, 0, out value) && Math.Abs(value - 50) < 0.001, "Midpoint must average symmetric readings");
        Check(MoistureField.Estimate(samples, 0.75f, 0, 0, out value) && value > 70 && value < 80, "Closer sensor must dominate");
        samples[1].valid = false;
        Check(MoistureField.Estimate(samples, 0, 0, 0, out value) && value == 20, "Stale sensor must not influence field");
        samples[0].valid = false;
        Check(!MoistureField.Estimate(samples, 0, 0, 0, out value), "No sensors must produce no estimate");
        for (int i = 0; i < samples.Length; i++) { samples[i].valid = true; samples[i].percent = 42; }
        for (int i = -20; i <= 20; i++)
            Check(MoistureField.Estimate(samples, i * 0.1f, 0.2f, -0.3f, out value) && Math.Abs(value - 42) < 0.001, "Uniform data must give uniform field");
        Console.WriteLine("PASS: sensor validation, expiry, wetting, 6,000 demo-range checks, exact samples, spatial weighting, missing sensors, uniform field.");
    }
}
