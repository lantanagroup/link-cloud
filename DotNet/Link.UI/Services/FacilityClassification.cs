namespace Link.UI.Services;

/// <summary>
/// One read the home page uses to separate real facilities from automation facilities.
/// </summary>
public interface IFacilityClassification
{
    Task<FacilityClassification> ReadAsync(CancellationToken cancellationToken);
}

public sealed class FacilityClassification
{
    public bool AutomationVisible { get; init; }
    public bool OwnershipReachable { get; init; }
    public AutomationOwnershipIndex Ownership { get; init; } = AutomationOwnershipIndex.Empty;

    /// <summary>Tenant list scope for real facilities. Null until the shared classification supplies one.</summary>
    public string? RealScope { get; init; }
}

/// <summary>
/// Delegates to <see cref="AutomationOwnershipLookup"/> and keeps automation visible.
/// Replace this type with the shared classification and LinkUi:AutomationEnabled when that flag is in this build.
/// </summary>
public sealed class OwnershipFacilityClassification : IFacilityClassification
{
    private readonly AutomationOwnershipLookup _ownership;

    public OwnershipFacilityClassification(AutomationOwnershipLookup ownership)
    {
        _ownership = ownership;
    }

    public async Task<FacilityClassification> ReadAsync(CancellationToken cancellationToken)
    {
        var (index, reachable) = await _ownership.GetSnapshotAsync(cancellationToken);
        return new FacilityClassification
        {
            AutomationVisible = true,
            OwnershipReachable = reachable,
            Ownership = index,
            RealScope = null
        };
    }
}
