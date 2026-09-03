namespace LantanaGroup.Link.DMRP.Data.Entities;

/// <summary>
/// The NHSN components DMRP reports a facility's enrollment under.
/// </summary>
/// <remarks>
/// Both are reported monthly and carry a month. The "annual" in the patient-safety endpoint's path
/// (<c>/ps/annual/mrp</c>) is part of its name, not a statement about its cadence - the two
/// operations return the same shape of data, and the component is what says which one an enrollment
/// came from.
/// </remarks>
public static class ReportingComponents
{
    /// <summary>Medicine reports.</summary>
    public const string Msc = "MSC";

    /// <summary>Patient safety.</summary>
    public const string Ps = "PS";

    public static readonly string[] All = [Msc, Ps];

    public static bool IsKnown(string? component) =>
        All.Contains(component, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The canonical casing of a known component, or the input unchanged if it is not one. Callers
    /// validate with <see cref="IsKnown"/> first.
    /// </summary>
    public static string Normalize(string? component) =>
        All.FirstOrDefault(c => string.Equals(c, component, StringComparison.OrdinalIgnoreCase))
        ?? component
        ?? string.Empty;
}
