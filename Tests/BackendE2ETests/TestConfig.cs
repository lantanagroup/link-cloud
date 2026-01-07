using System.Reflection;

namespace LantanaGroup.Link.Tests.E2ETests;

public static class TestConfig
{
    public static string ExternalFhirServerBase => Environment.GetEnvironmentVariable("EXTERNAL_FHIR_SERVER_BASE_URL") ?? "http://localhost:6157/fhir";
    public static string InternalFhirServerBase => Environment.GetEnvironmentVariable("INTERNAL_FHIR_SERVER_BASE_URL") ?? "http://fhir-server:8080/fhir";
    public static string AdminBffBase => Environment.GetEnvironmentVariable("ADMIN_BFF_BASE_URL") ?? "http://localhost:8063/api";
    public static string? SmokeTestDownloadPath =>
    Environment.GetEnvironmentVariable("SMOKE_TEST_DOWNLOAD_PATH");
    public static bool CleanupSmokeTestData => bool.Parse(Environment.GetEnvironmentVariable("CLEANUP_SMOKE_TEST_DATA") ?? "true");
    public static OAuthConfig AdminBffOAuth => new("ADMINBFF");
    public static OAuthConfig FhirServerOAuth => new("FHIRSERVER");
    public static BasicAuthConfig FhirServerBasicAuth => new("FHIRSERVER");
    public static SmokeTestConfig AdhocReportingSmokeTestConfig => new("ADHOC_REPORTING_SMOKE_TEST");
    public const string AdHocSmokeTestFile = "Stu3-AdHocSmokeTest";
    public const string SingleMeasureAdHocFacility = "SingleMeasureAdHocFacility";
    public const string SingleMeasureAdHocAchDqmVersion = "1.0.0-dev";
    public const string MeasureAch = "NHSNAcuteCareHospitalMonthlyInitialPopulation";
    public const string CronValue = "0 0 */4 * * ?";

    public const string singleMeasureAdHocTestPatient_One = "patient-CYUcGIlSrpJxCBMeEml30YSmE0Ea7loNBPVZfhCUkv7A3.ndjson";
    public const string singleMeasureAdHocTestPatient_Two = "patient-6tZ8Wt8maJdDFLvEsDcKmAaCAcSOxjr0mB8RjEi5Szw7H.ndjson";
    public const string singleMeasureAdHocTestPatient_Three = "patient-jjMZxCVWUbZgLkPf2LTzvZIBOW76YLJdIGCw8JFaTPiZg.ndjson";
    public const string singleMeasureAdHocTestPatient_Four = "patient-MVLkMLWErl3gQGRCuA2mygtVuix7PMBFBh9WVayaCL7xM.ndjson";
    public const string singleMeasureAdHocTestPatient_Five = "patient-VsZkAG8h9vkGcL528ZcJxVXynyj8X39GaDfjHbA9AnvyA.ndjson";
    public const string singleMeasureAdHocTestPatient_Six = "patient-x25sJU80vVa51mxJ6vSDcjbNC3BcdCQujJbXQwqdppFOO.ndjson";
    public const string singleMeasureAdHocTestPatient_Seven = "patient-jbbPDJeGWyEyudcf6EBKTgmeCLxB7jTgu5Ugm27JAO494.ndjson";
    public const string singleMeasureAdHocTestPatient_Eight = "patient-DJxsHpmWuBezhV9hJNgEHT4szaKW3uP5vUNzXUCkltpXj.ndjson";
    public const string singleMeasureAdHocTestPatient_Nine = "patient-9i6Xi6uG2WjuGxHTmpbin4ct2ZwevRwTWhIkJkRjVFZ4C.ndjson";
    public const string singleMeasureAdHocTestPatient_Ten = "patient-5ieWogP3EGV24Kus8QsGh6rpmUaJBP5Hl0nCSJJXmh6TI.ndjson";

    public static class FhirQueryConfig
    {
        public const int MaxConcurrentRequests = 5;
        public static readonly TimeSpan MinAcquisitionPullTime = TimeSpan.FromHours(1);
        public static readonly TimeSpan MaxAcquisitionPullTime = TimeSpan.FromHours(24);
        public static readonly string TimeZone = "America/New_York";
    }

    public static readonly string[] SingleMeasureExpectedFiles =
    {
        "manifest.ndjson",
        "patient-x25sJU80vVa51mxJ6vSDcjbNC3BcdCQujJbXQwqdppFOO.ndjson",
        "patient-MVLkMLWErl3gQGRCuA2mygtVuix7PMBFBh9WVayaCL7xM.ndjson",
        "patient-CYUcGIlSrpJxCBMeEml30YSmE0Ea7loNBPVZfhCUkv7A3.ndjson",
        "patient-VsZkAG8h9vkGcL528ZcJxVXynyj8X39GaDfjHbA9AnvyA.ndjson",
        "patient-jjMZxCVWUbZgLkPf2LTzvZIBOW76YLJdIGCw8JFaTPiZg.ndjson",
        "patient-6tZ8Wt8maJdDFLvEsDcKmAaCAcSOxjr0mB8RjEi5Szw7H.ndjson"
    };

    public static readonly string[] SingleMeasureExpectedPatientIds =
        SingleMeasureExpectedFiles
            .Where(f => f.StartsWith("patient-", StringComparison.OrdinalIgnoreCase))
            .Select(f => f.Substring("patient-".Length, f.Length - "patient-".Length - ".ndjson".Length))
            .ToArray();

    public static string GetEmbeddedResourceContent(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new FileNotFoundException($"Embedded resource '{resourceName}' not found.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    public class SmokeTestConfig(string prefix)
    {
        public string MeasureBundleLocation => Environment.GetEnvironmentVariable($"{prefix}_MEASURE_BUNDLE_PATH") ?? "resource://LantanaGroup.Link.Tests.BackendE2ETests.measures.NHSNAcuteCareHospitalMonthlyInitialPopulation.json";
        public string StartDate => Environment.GetEnvironmentVariable($"{prefix}_START_DATE") ?? "2023-01-01T00:00:00Z";
        public string EndDate => Environment.GetEnvironmentVariable($"{prefix}_END_DATE") ?? "2023-12-31T23:59:59Z";
        public List<string> PatientIds = Environment.GetEnvironmentVariable($"{prefix}_PATIENT_IDS")?.Split(',')?.ToList() ?? ["207727"];
        public bool RemoveFacilityConfig = bool.Parse(Environment.GetEnvironmentVariable($"{prefix}_REMOVE_FACILITY_CONFIG") ?? "true");
        public bool RemoveReport = Environment.GetEnvironmentVariable($"{prefix}_REMOVE_REPORT")?.ToLower() == "true";
    }
    public class BasicAuthConfig(string prefix)
    {
        public bool ShouldAuthenticate { get; } = bool.Parse(Environment.GetEnvironmentVariable($"{prefix}_BASICAUTH_SHOULD_AUTHENTICATE") ?? "false");
        public string? Username { get; } = Environment.GetEnvironmentVariable($"{prefix}_BASICAUTH_USERNAME");
        public string? Password { get; } = Environment.GetEnvironmentVariable($"{prefix}_BASICAUTH_PASSWORD");
    }
    public class OAuthConfig(string prefix)
    {
        public bool ShouldAuthenticate { get; } = bool.Parse(Environment.GetEnvironmentVariable($"{prefix}_OAUTH_SHOULD_AUTHENTICATE") ?? "false");
        public string? TokenEndpoint { get; } = Environment.GetEnvironmentVariable($"{prefix}_OAUTH_TOKEN_ENDPOINT");
        public string? ClientId { get; } = Environment.GetEnvironmentVariable($"{prefix}_OAUTH_CLIENT_ID");
        public string? ClientSecret { get; } = Environment.GetEnvironmentVariable($"{prefix}_OAUTH_CLIENT_SECRET");
        public string Scope { get; } = Environment.GetEnvironmentVariable($"{prefix}_OAUTH_SCOPE") ?? "openid profile email";
        public string? Username { get; } = Environment.GetEnvironmentVariable($"{prefix}_OAUTH_USERNAME");
        public string? Password { get; } = Environment.GetEnvironmentVariable($"{prefix}_OAUTH_PASSWORD");
    }
    public static class TestContextStore
    {
        private static readonly AsyncLocal<string?> _reportTrackingIdGuid = new();
        private static readonly AsyncLocal<string?> _adHocReportTrackingIdGuid = new();

        public static string? ReportTrackingIdGuid
        {
            get => _reportTrackingIdGuid.Value;
            set => _reportTrackingIdGuid.Value = value;
        }

        public static string? AdHocReportTrackingIdGuid
        {
            get => _adHocReportTrackingIdGuid.Value;
            set => _adHocReportTrackingIdGuid.Value = value;
        }
    }
    public static class ValidationHelper
    {
        /// <summary>
        /// Attempts to run a validation method. Captures and logs, does not stop test. 
        /// </summary>
        //public static void TryRunValidation(Action validationMethod, List<string> failures)
        //{
        //    try
        //    {
        //        validationMethod();
        //    }
        //    catch (Exception ex)
        //    {
        //        string methodName = validationMethod.Method.Name;
        //        Console.WriteLine($"[FAIL] {methodName} - {ex.Message}");
        //        failures.Add($"{methodName}: {ex.Message}");
        //    }
        //}

        /// <summary>
        /// Async version for use with asynchronous validations.
        /// </summary>
        //public static async Task TryRunValidationAsync(Func<Task> validationMethod, List<string> failures)
        //{
        //    try
        //    {
        //        await validationMethod();
        //    }
        //    catch (Exception ex)
        //    {
        //        string methodName = validationMethod.Method.Name;
        //        Console.WriteLine($"[FAIL] {methodName} - {ex.Message}");
        //        failures.Add($"{methodName}: {ex.Message}");
        //    }
        //}

        public enum ValidationSeverity
        {
            Info,
            Warning,
            Error
        }

        public enum ValidationIssueType
        {
            MissingResourceTypeInBundle,
            BundleCountMismatch,
            EvaluatedCountMismatch,
            UnexpectedEvaluatedType,
            ExcludedTypeMissingFromBundle,
            ExcludedTypePresentInEvaluated,
            OperationOutcomePresentInEvaluated,
            BundleVsEvaluatedCountMismatch,
            BundleVsEvaluatedIdMismatch,
            CrossTypeReference,
            ObservedResourceTypeCount
        }

        public sealed class ValidationIssue
        {
            public string FileName { get; }
            public ValidationIssueType IssueType { get; }
            public ValidationSeverity Severity { get; }
            public string ResourceType { get; }
            public string Message { get; }

            public ValidationIssue(
                string fileName,
                ValidationIssueType issueType,
                ValidationSeverity severity,
                string resourceType,
                string message)
            {
                FileName = fileName;
                IssueType = issueType;
                Severity = severity;
                ResourceType = resourceType;
                Message = message;
            }
        }
    }
}