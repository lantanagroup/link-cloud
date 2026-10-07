namespace Link.UI.Services;

/// <summary>
/// The shared acquisition-log panel.
/// A run entry exists only while automation is on.
/// A named facility is listed directly, so the Real default on the unscoped page does not hide it.
/// Log actions are the existing facility and report commands. This type does not add another gate.
/// </summary>
public static class AcquisitionLogPanelRules
{
    public static bool AllowsRunEntry(bool automationEnabled) => automationEnabled;

    public static string? ScopeForEntry(bool namedFacility) =>
        namedFacility ? AutomationMarkRules.All : null;
}
