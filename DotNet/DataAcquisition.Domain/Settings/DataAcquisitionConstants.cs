
namespace LantanaGroup.Link.DataAcquisition.Domain.Settings;

public static class DataAcquisitionConstants
{
    public const string ServiceName = "DataAcquisition";

    public static class AppSettingsSectionNames
    {
        public const string ServiceInformation = "ServiceInformation";
        public const string ExternalConfigurationSource = "ExternalConfigurationSource";
        public const string DatabaseProvider = "DatabaseProvider";
        public const string Serilog = "Serilog";
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
        public const string ReportId = "X-Report-Tracking-Id";
    }

    public static class ContentTypes
    {
        public const string FhirJson = "application/fhir+json";
    }

    public static class Extension
    {
        public const string CdcUri = "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/link-received-date-extension";
    }

    public static class LoggingIds
    {
        public const int GenerateItems = 1000;
        public const int ListItems = 1001;
        public const int GetItem = 1002;
        public const int InsertItem = 1003;
        public const int UpdateItem = 1004;
        public const int DeleteItem = 1005;
        public const int GetItemNotFound = 1006;
        public const int UpdateItemNotFound = 1007;
        public const int KafkaConsumer = 10008;
        public const int KafkaProducer = 10009;
        public const int HealthCheck = 10010;
    }
}
