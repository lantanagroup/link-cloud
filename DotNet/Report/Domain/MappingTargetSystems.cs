namespace LantanaGroup.Link.Report.Domain;

public static class MappingTargetSystems
{
    public const string HslocUrl = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";
    public const string HslocOid = "urn:oid:2.16.840.1.113883.6.259";

    public static bool IsHsloc(string? targetSystem) =>
        targetSystem?.Trim() is { } s &&
        (s.Equals(HslocUrl, StringComparison.Ordinal) || s.Equals(HslocOid, StringComparison.Ordinal));
}