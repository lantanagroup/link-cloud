using System.Reflection;

namespace LantanaGroup.Link.Tests.E2ETests;

public static class TestConfig
{
    public static string ExternalFhirServerBase => Environment.GetEnvironmentVariable("EXTERNAL_FHIR_SERVER_BASE_URL") ?? "http://localhost:6157/fhir";
    public static string InternalFhirServerBase => Environment.GetEnvironmentVariable("INTERNAL_FHIR_SERVER_BASE_URL") ?? "http://fhir-server:8080/fhir";
    public static string AdminBffBase => Environment.GetEnvironmentVariable("ADMIN_BFF_BASE_URL") ?? "http://localhost:8063/api";
    public static string AdhocReportingSmokeTestMeasureBundleLocation => Environment.GetEnvironmentVariable("ADHOC_REPORTING_SMOKE_TEST_MEASURE_BUNDLE_PATH") ?? "resource://LantanaGroup.Link.Tests.E2ETests.measures.NHSNdQMAcuteCareHospitalInitialPopulation.json";
    public static OAuthConfig AdminBffOAuth => new("ADMINBFF");
    public static OAuthConfig FhirServerOAuth => new("FHIRSERVER");
    public static BasicAuthConfig FhirServerBasicAuth => new("FHIRSERVER");

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
}