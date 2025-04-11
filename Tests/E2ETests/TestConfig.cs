namespace LantanaGroup.Link.Tests.E2ETests;

public class TestConfig
{
    public static string FhirServerBase => Environment.GetEnvironmentVariable("FHIR_SERVER_BASE_URL") ?? "http://localhost:6157/fhir";

    public static string AdminBffBase => Environment.GetEnvironmentVariable("ADMIN_BFF_BASE_URL") ?? "http://localhost:8063/api";
    public static string MeasureBundleLocation => Environment.GetEnvironmentVariable("MEASURE_BUNDLE_PATH") ?? "resource://LantanaGroup.Link.Tests.E2ETests.measures.NHSNdQMAcuteCareHospitalInitialPopulation.json";
}