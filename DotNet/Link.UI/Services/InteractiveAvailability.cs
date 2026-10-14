namespace Link.UI.Services;

/// <summary>
/// Decides whether Interactive mode can load a patient's submitted bundle on demand. It only uses
/// facts that are already at hand or one existence check; it never loads the submission to decide.
/// </summary>
public static class InteractiveAvailability
{
    public const string NotConfigured = "Submitted bundles cannot be read from ABS here, so Interactive mode is not available.";
    public const string NoReport = "This run did not create a report, so there is no submission for Interactive mode to load.";
    public const string CleanedUp = "This run cleaned up after itself, so its submission is no longer in ABS and Interactive mode is not available.";
    public const string ReportGone = "The submission for this report is no longer in ABS, so Interactive mode is not available.";

    /// <summary>Whether the run's report needs an existence check at all.</summary>
    public static bool NeedsCheck(bool storeConfigured, string? facilityId, string? reportId) =>
        storeConfigured && !string.IsNullOrWhiteSpace(facilityId) && Guid.TryParse(reportId, out _);

    /// <summary>
    /// The tooltip text when Interactive mode is off for an Automation run, or null when it can load.
    /// <paramref name="reportExists"/> is null when the Report service could not be asked; the
    /// button then stays on and a failed load says why inline.
    /// </summary>
    public static string? ForRun(bool storeConfigured, string? facilityId, string? reportId, bool? reportExists)
    {
        if (!storeConfigured)
            return NotConfigured;
        if (string.IsNullOrWhiteSpace(facilityId) || !Guid.TryParse(reportId, out _))
            return NoReport;
        return reportExists == false ? CleanedUp : null;
    }
}
