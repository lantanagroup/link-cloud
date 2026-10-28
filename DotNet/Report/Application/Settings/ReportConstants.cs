namespace LantanaGroup.Link.Report.Settings
{
    public static class ReportConstants
    {
        public const string ServiceName = "Report";

        public static class AppSettingsSectionNames
        {
            public const string ServiceInformation = "ServiceInformation";
        }

        public static class BundleSettings
        {
            public const string ApplicablePeriodExtensionUrl = "https://www.cdc.gov/nhsn/nhsn-measures/StructureDefinition/link-patient-list-applicable-period-extension";
            public const string BundlingUrlBase = "https://www.cdc.gov/nhsn/nhsn-measures";
            public const string BundlingFullUrlFormat = BundlingUrlBase + "/{0}";
            public const string CdcOrgIdSystem = "https://www.cdc.gov/nhsn/OrgID";
            public const string CensusProfileUrl = "http://hl7.org/fhir/us/nhsn-dqm/StructureDefinition/poi-list";
            public const string DataAbsentReasonExtensionUrl = "http://hl7.org/fhir/StructureDefinition/data-absent-reason";
            public const string DataAbsentReasonUnknownCode = "unknown";
            public const string IdentifierSystem = "urn:ietf:rfc:3986";
            public const string IndividualMeasureReportProfileUrl = "http://hl7.org/fhir/us/davinci-deqm/StructureDefinition/indv-measurereport-deqm";
            public const string MainSystem = "https://nhsnlink.org";
            public const string NationalProviderIdentifierSystemUrl = "http://hl7.org.fhir/sid/us-npi";
            public const string OrganizationTypeSystem = "http://terminology.hl7.org/CodeSystem/organization-type";
            public const string ReportBundleProfileUrl = "https://www.cdc.gov/nhsn/nhsn-measures/StructureDefinition/nhsn-measurereport-bundle";
            public const string SubjectListMeasureReportProfile = "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/subjectlist-measurereport";
            public const string SubmittingDeviceProfile = "http://hl7.org/fhir/us/nhsn-dqm/StructureDefinition/nhsn-submitting-device";
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

        public static class LoggingIds
        {
            public const int GenerateItems = 1000;
            public const int SearchPerformed = 1001;
            public const int GetItem = 1002;
            public const int InsertItem = 1003;
            public const int UpdateItem = 1004;
            public const int DeleteItem = 1005;
            public const int GetItemNotFound = 1006;
            public const int UpdateItemNotFound = 1007;
            public const int SearchException = 1008;
            public const int GetItemException = 1009;
            public const int EventConsumerInit = 2000;
            public const int EventConsumerObserved = 2001;
            public const int EventConsumerException = 2002;
            public const int EventConsumerOperationCanceled = 2003;
            public const int HealthCheck = 10010;
        }
    }
}
