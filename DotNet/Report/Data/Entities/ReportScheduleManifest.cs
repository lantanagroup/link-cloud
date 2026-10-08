namespace LantanaGroup.Link.Report.Data.Entities;

/// <summary>
/// Cross-pod claim for one report manifest. Emitted is terminal.
/// A claimed row can be taken again only after <see cref="ClaimLease"/>.
/// </summary>
public static class ReportScheduleManifest
{
    public const int None = 0;
    public const int Claimed = 1;
    public const int Emitted = 2;

    public static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(10);
}
