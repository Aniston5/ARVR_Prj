using System;

/// <summary>Independent sensor slots; caller supplies timestamps in seconds.</summary>
public sealed class SoilMoistureProfile
{
    public const int PlantCount = 4, DepthCount = 3;
    private readonly float[,] values = new float[PlantCount, DepthCount];
    private readonly double[,] times = new double[PlantCount, DepthCount];
    private readonly bool[,] received = new bool[PlantCount, DepthCount];

    public bool Set(int plant, int depth, float percent, double now)
    {
        if (plant < 0 || plant >= PlantCount || depth < 0 || depth >= DepthCount ||
            float.IsNaN(percent) || float.IsInfinity(percent) || percent < 0 || percent > 100 ||
            double.IsNaN(now) || double.IsInfinity(now)) return false;
        values[plant, depth] = percent;
        times[plant, depth] = now;
        received[plant, depth] = true;
        return true;
    }

    public bool TryGet(int plant, int depth, double now, double timeout, out float value)
    {
        value = 0;
        if (plant < 0 || plant >= PlantCount || depth < 0 || depth >= DepthCount ||
            !received[plant, depth] || now < times[plant, depth] || now - times[plant, depth] > timeout)
            return false;
        value = values[plant, depth];
        return true;
    }

    public void Clear() { Array.Clear(received, 0, received.Length); }

    // Illustrative wetting response, not a soil-physics model.
    public static float DemoValue(int plant, int depth, double secondsAfterWatering)
    {
        float baseline = 24 + plant * 7 - depth * 5;
        double elapsed = secondsAfterWatering - depth * 3;
        if (elapsed <= 0) return baseline;
        double rise = 1 - Math.Exp(-elapsed / 2.5);
        double drain = Math.Exp(-Math.Max(0, elapsed - 14) / 24);
        return (float)Math.Min(100, baseline + (68 - depth * 7) * rise * drain);
    }
}
