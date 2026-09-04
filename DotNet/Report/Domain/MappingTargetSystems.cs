namespace LantanaGroup.Link.Report.Domain;

public static class MappingTargetSystems
{
    public const string HslocUrl = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";
    public const string HslocOid = "urn:oid:2.16.840.1.113883.6.259";

    /// <summary>
    /// Whether a configured code map targets HSLOC, and so answers the report's HSLOC indicator.
    /// </summary>
    /// <remarks>
    /// The comparison ignores case because the target is an operator-entered string and neither of these
    /// identifiers has a case-sensitive part that distinguishes it from anything else -- a facility that
    /// typed <c>URN:OID:...</c> configured HSLOC. Matching ordinally would report the column as
    /// <c>NotApplicable</c>, which claims nothing is configured for it, and the operator would be looking
    /// for a missing code map that is in fact there.
    ///
    /// It stays an exact match otherwise. A target that differs by scheme, host or a trailing slash is a
    /// different system as far as this is concerned, so a genuine misconfiguration still reports as
    /// unconfigured rather than being quietly accepted.
    /// </remarks>
    public static bool IsHsloc(string? targetSystem) =>
        targetSystem?.Trim() is { } s &&
        (s.Equals(HslocUrl, StringComparison.OrdinalIgnoreCase) ||
         s.Equals(HslocOid, StringComparison.OrdinalIgnoreCase));
}