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

        // Only a missing ServiceRequest or Observation key. A type= surplus
        // (actual greater than expected) and a "Resource ServiceRequest/... has ..."
        // reference error are deterministic and must fail the test immediately.
        return error.Contains("ABS artifacts missing expected resource: ServiceRequest/", StringComparison.Ordinal)
            || error.Contains("ABS artifacts missing expected resource: Observation/", StringComparison.Ordinal);
    }
}
