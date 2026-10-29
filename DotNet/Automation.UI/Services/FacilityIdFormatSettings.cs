namespace Automation.UI.Services;

/// <summary>
/// Tenant's facility-id format flag. The configuration key is
/// <see cref="NumericOnlyKey"/>. Tenant binds the same section. A missing row
/// leaves the flag off, which is the value in Tenant appsettings and in the
/// deployed stores that do not carry the key.
/// </summary>
public sealed class FacilityIdFormatSettings
{
    public const string SectionName = "FacilityIdSettings";
    public const string NumericOnlyKey = SectionName + ":NumericOnlyFacilityId";

    /// <summary>
    /// App Configuration label on Tenant's own rows. Tenant loads unlabeled rows
    /// first, then this label. The dev, qa, qa2, and test stores have no
    /// <see cref="NumericOnlyKey"/> row, so the appsettings default stays in effect.
    /// </summary>
    public const string TenantLabel = "Tenant";

    public bool NumericOnlyFacilityId { get; set; }

    /// <summary>
    /// The Tenant-labeled row for <see cref="NumericOnlyKey"/>. Loaded after the
    /// unlabeled row so it overrides it, matching Tenant's own select order.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, string>> TenantOverrideLabels()
        => [new KeyValuePair<string, string>(NumericOnlyKey, TenantLabel)];
}
