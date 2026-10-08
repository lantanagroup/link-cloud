namespace LantanaGroup.Link.Report.Data.Entities;

/// <summary>
/// Cross-pod claim for one report manifest. Emitted is terminal.
/// A claimed row can be taken again only after <see cref="ClaimLease"/>.
/// Release and emitted updates must match the claim token.
/// </summary>
public static class ReportScheduleManifest
{
    public const int None = 0;
    public const int Claimed = 1;
    public const int Emitted = 2;

    public static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(10);
}

public enum ManifestClaimResult
{
    Won,
    AlreadyEmitted,
    Held
}
