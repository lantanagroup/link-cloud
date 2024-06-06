namespace LantanaGroup.Link.Report.Settings
{
    public static class ReportConstants
    {
        public const string ServiceName = "Report";

        public static class AppSettingsSectionNames
        {
            public const string Mongo = "MongoDB";
            public const string ServiceInformation = "ServiceInformation";
            public const string Telemetry = "TelemetryConfig";
            public const string TenantApiSettings = "TenantApiSettings";
            public const string ExternalConfigurationSource = "ExternalConfigurationSource";
            public const string EnableSwagger = "EnableSwagger";
        }

        public static class BundleSettings
        {
            public const string ApplicablePeriodExtensionUrl = "https://www.cdc.gov/nhsn/nhsn-measures/StructureDefinition/link-patient-list-applicable-period-extension";
            public const string BundlingFullUrlFormat = "https://www.cdc.gov/nhsn/nhsn-measures/{0}";
            public const string CdcOrgIdSystem = "https://www.cdc.gov/nhsn/OrgID";
            public const string CensusProfileUrl = "https://www.cdc.gov/nhsn/nhsn-measures/StructureDefinition/poi-list";
            public const string DataAbsentReasonExtensionUrl = "http://hl7.org/fhir/StructureDefinition/data-absent-reason";
            public const string DataAbsentReasonUnknownCode = "unknown";
            public const string IdentifierSystem = "urn:ietf:rfc:3986";
            public const string IndividualMeasureReportProfileUrl = "http://hl7.org/fhir/us/davinci-deqm/StructureDefinition/indv-measurereport-deqm";
            public const string MainSystem = "https://nhsnlink.org";
            public const string NationalProviderIdentifierSystemUrl = "http://hl7.org.fhir/sid/us-npi";
            public const string OrganizationTypeSystem = "http://terminology.hl7.org/CodeSystem/organization-type";
            public const string ReportBundleProfileUrl = "https://www.cdc.gov/nhsn/nhsn-measures/StructureDefinition/nhsn-measurereport-bundle";
            public const string SubjectListMeasureReportProfile = "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/subjectlist-measurereport";
            public const string SubmittingOrganizationProfile = "https://www.cdc.gov/nhsn/nhsn-measures/StructureDefinition/nhsn-submitting-organization";
        }

        public static class MeasureReportSubmissionScheduler
        {
            public const string ReportScheduleModel = "ReportScheduleModel";
            public const string Group = "MeasureReportSubmissionGroup";
        }

        public static class MeasureReportLoggingIds
        {
            public const int HealthCheck = 10010;
        }

    }
}
