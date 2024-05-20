
namespace LantanaGroup.Link.Normalization.Application.Settings;

public static class NormalizationConstants
{
    public const string ServiceName = "Normalization";

    public static class AppSettingsSectionNames
    {
        public const string ServiceInformation = "ServiceInformation";
        public const string DatabaseProvider = "DatabaseProvider";
        public const string ExternalConfigurationSource = "ExternalConfigurationSource";
    }

    public static class FixResourceIDCommand
    {
        public const string UuidPrefix = "urn:uuid:";
        public const string ORIG_ID_EXT_URL = "https://www.cdc.gov/nhsn/fhir/nhsnlink/StructureDefinition/nhsnlink-original-id";
    }

    public static class ConceptMap
    {
        public const string ExtensionName = "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/link-original-mapped-concept-extension";
    }

    public static class Extensions
    {
        public const string OriginalElementValueExtension = "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/link-original-element-value-extension";
    }

    public static class HeaderNames
    {
        public const string CorrelationId = "X-Correlation-Id";
    }

    public static class NormalizationLoggingIds
    {
        public const int HealthCheck = 10010;
    }
}
