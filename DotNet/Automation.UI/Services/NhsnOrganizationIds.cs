using System.Globalization;

namespace Automation.UI.Services;

/// <summary>
/// NHSN organization ids Automation assigns. Five digits, 10000–99999.
/// 10756–10765 were baked into every system scenario and then copied onto
/// new scenarios, so DMRP runs kept scheduling facility 10765.
/// </summary>
public static class NhsnOrganizationIds
{
    public const int Minimum = 10000;
    public const int MaximumExclusive = 100000;

    /// <summary>Historical system-scenario ids. Never assign these again.</summary>
    public static readonly IReadOnlySet<string> ReservedHistorical = new HashSet<string>(StringComparer.Ordinal)
    {
        "10756", "10757", "10758", "10759", "10760",
        "10761", "10762", "10763", "10764", "10765"
    };

    public static bool IsReserved(string? id) =>
        !string.IsNullOrWhiteSpace(id) && ReservedHistorical.Contains(id.Trim());

    public static bool IsValid(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        var trimmed = id.Trim();
        if (trimmed.Length != 5 || !trimmed.All(char.IsDigit))
            return false;

        return int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            && value >= Minimum
            && value < MaximumExclusive;
    }

    public static string Generate(Random? random = null)
    {
        var value = (random ?? Random.Shared).Next(Minimum, MaximumExclusive);
        return value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Picks an id that is not reserved and not in <paramref name="taken"/>.
    /// </summary>
    public static string Allocate(IEnumerable<string>? taken, Random? random = null)
    {
        var blocked = new HashSet<string>(ReservedHistorical, StringComparer.Ordinal);
        if (taken != null)
        {
            foreach (var id in taken)
            {
                if (!string.IsNullOrWhiteSpace(id))
                    blocked.Add(id.Trim());
            }
        }

        for (var attempt = 0; attempt < 64; attempt++)
        {
            var id = Generate(random);
            if (!blocked.Contains(id))
                return id;
        }

        throw new InvalidOperationException("Could not allocate a unique NHSN organization id.");
    }
}
