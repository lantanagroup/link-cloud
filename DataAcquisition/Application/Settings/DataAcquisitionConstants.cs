using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;

namespace LantanaGroup.Link.DataAcquisition.Application.Settings;

public static class DataAcquisitionConstants
{
    public const string ServiceName = "DataAcquisition";

    public static class AppSettingsSectionNames
    {
        public const string Kafka = "KafkaConnection";
        public const string Mongo = "MongoDB";
        public const string Redis = "Redis";
        public const string ServiceInformation = "ServiceInformation";
        public const string Telemetry = "TelemetryConfig";
    }

    public static class ValidationErrorMessages
    {
        public const string NullConfigModel = "Config Model is null.";
        public const string NullTenantId = "TenantId is null.";
        public const string NullFacilities = "Facilities is null.";
        public const string NoFacilities = "No Facilities Configured.";
        public const string NullFacilityId = "Facility ID is null.";
        public const string NullResourceSettings = "ResourceSettings is null.";
        public const string NoResourceSettings = "No Resource Settings exist for Facility";
    }

    public static class MessageNames
    {
        public const string DataAcquisitionScheduled = "DataAcquisitionScheduled";
        public const string PatientCensusScheduled = "PatientCensusScheduled";
        public const string DataAcquisitionRequested = "DataAcquisitionRequested";
    }

    public static class HeaderNames
    {
        public const string CorrelationId = "X-Correlation-Id";
    }

    public static class ContentTypes
    {
        public const string FhirJson = "application/fhir+json";
    }

    public static class Extension
    {
        public const string CdcUri = "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/link-received-date-extension";
    }
}
