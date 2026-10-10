using System;

/// <summary>Inverse-distance-squared estimation in local metres, ignoring missing sensors.</summary>
public static class MoistureField
{
    public struct Sample
    {
        public float x, y, z, percent;
        public bool valid;
    }

    public static bool Estimate(Sample[] samples, float x, float y, float z, out float percent)
    {
        double weighted = 0, weights = 0, coincident = 0;
        int coincidentCount = 0;
        foreach (var sample in samples)
        {
            if (!sample.valid) continue;
            double dx = x - sample.x, dy = y - sample.y, dz = z - sample.z;
            double distanceSquared = dx * dx + dy * dy + dz * dz;
            if (distanceSquared < 1e-10) { coincident += sample.percent; coincidentCount++; continue; }
            double weight = 1 / distanceSquared;
            weighted += weight * sample.percent;
            weights += weight;
        }
        percent = coincidentCount > 0 ? (float)(coincident / coincidentCount) : weights > 0 ? (float)(weighted / weights) : 0;
        return coincidentCount > 0 || weights > 0;
    }
}
