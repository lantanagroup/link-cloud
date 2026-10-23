using System.Globalization;
using LantanaGroup.Link.PatientFlowSimulator.Scenario;

namespace LantanaGroup.Link.PatientFlowSimulator.Planning;

/// <summary>
/// Seeded randomness. The same key always yields the same sequence, so a month can be regenerated
/// bit-for-bit and a later month can recompute an earlier month's patients.
/// </summary>
public static class DeterministicRandom
{
    // Admissions per local hour of day, 00:00 to 23:00. Mornings and afternoons are busiest.
    private static readonly int[] HourWeights =
    [
        1, 1, 1, 1, 1, 2,
        3, 4, 5, 6, 6, 6,
        6, 6, 6, 6, 6, 5,
        4, 3, 3, 2, 2, 1
    ];

    /// <summary>
    /// Creates a generator seeded from the given key parts.
    /// </summary>
    public static Random Create(params object[] parts)
    {
        var key = string.Join("|", parts.Select(p => Convert.ToString(p, CultureInfo.InvariantCulture)));
        return new Random(unchecked((int)Fnv1a32(key)));
    }

    /// <summary>
    /// 32-bit FNV-1a hash of a string. Stable across processes, unlike string.GetHashCode().
    /// </summary>
    public static uint Fnv1a32(string value)
    {
        unchecked
        {
            const uint offsetBasis = 2166136261;
            const uint prime = 16777619;

            var hash = offsetBasis;
            foreach (var c in value)
            {
                hash ^= c;
                hash *= prime;
            }

            return hash;
        }
    }

    /// <summary>
    /// An integer inside an inclusive range.
    /// </summary>
    public static int NextInt(this Random random, RangeSpec range) => random.Next(range.Min, range.Max + 1);

    /// <summary>
    /// A double inside a range.
    /// </summary>
    public static double NextDouble(this Random random, double min, double max)
        => min + random.NextDouble() * (max - min);

    /// <summary>
    /// A local hour of day, weighted towards daytime.
    /// </summary>
    public static int NextWeightedHourOfDay(this Random random)
    {
        var roll = random.Next(HourWeights.Sum());
        for (var hour = 0; hour < HourWeights.Length; hour++)
        {
            roll -= HourWeights[hour];
            if (roll < 0)
            {
                return hour;
            }
        }

        return HourWeights.Length - 1;
    }

    /// <summary>
    /// One item of a list.
    /// </summary>
    public static T Pick<T>(this Random random, IReadOnlyList<T> items) => items[random.Next(items.Count)];
}
