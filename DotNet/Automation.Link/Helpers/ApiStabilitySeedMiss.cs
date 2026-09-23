namespace LantanaGroup.Link.Automation.Link.Helpers;

/// <summary>
/// API Stability seeds one small scenario, then checks that data acquisition
/// landed every resource the manifest predicted. Under load — and sometimes on
/// a quiet stack — acquisition drops a ServiceRequest or Observation for that
/// seed while the rest of the patient file is present. The end-to-end test
/// retries only that miss.
/// </summary>
public static class ApiStabilitySeedMiss
{
    public static bool IsRetryable(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return false;

        if (!error.Contains("REPORT INTERNAL ABS MANIFEST VALIDATION", StringComparison.Ordinal))
            return false;

        return error.Contains("type=ServiceRequest:", StringComparison.Ordinal)
            || error.Contains("type=Observation:", StringComparison.Ordinal)
            || error.Contains("ServiceRequest/", StringComparison.Ordinal)
            || error.Contains("Observation/", StringComparison.Ordinal);
    }
}
