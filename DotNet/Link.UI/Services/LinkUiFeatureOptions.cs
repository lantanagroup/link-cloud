namespace Link.UI.Services;

/// <summary>
/// Local switches that must match the Tenant service this UI talks to.
/// Both default off: DMRP off keeps scheduled-report fields visible (an on-flag that failed
/// to load would quietly save facilities with an empty schedule), and facility ids follow
/// Tenant's alphanumeric default.
/// </summary>
public sealed class LinkUiFeatureOptions
{
    public bool DmrpEnabled { get; set; }
    public bool NumericOnlyFacilityId { get; set; }

    /// <summary>
    /// Automation runs, cleanup, and the automation nav.
    /// Off in production. On for local Development. Test and QA set this true in their own settings.
    /// </summary>
    public bool AutomationEnabled { get; set; }
}
