using System.Reflection;

namespace LantanaGroup.Link.Tests.E2ETests;

public class TestConfig
{
    public static string ExternalFhirServerBase => Environment.GetEnvironmentVariable("FHIR_SERVER_BASE_URL") ?? "http://localhost:6157/fhir";
    public static string InternalFhirServerBase => Environment.GetEnvironmentVariable("FHIR_SERVER_BASE_URL") ?? "http://fhir-server:8080/fhir";

    public static string AdminBffBase => Environment.GetEnvironmentVariable("ADMIN_BFF_BASE_URL") ?? "http://localhost:8063/api";
    public static string MeasureBundleLocation => Environment.GetEnvironmentVariable("MEASURE_BUNDLE_PATH") ?? "resource://LantanaGroup.Link.Tests.E2ETests.measures.NHSNdQMAcuteCareHospitalInitialPopulation.json";

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
}