namespace LantanaGroup.Link.Submission.Settings
{
    public static class SubmissionConstants
    {

        public const string ServiceName = "Submission";
        public const string BundlingFullUrlFormat = "https://www.cdc.gov/nhsn/nhsn-measures/{0}";

        public static class AppSettingsSectionNames
        {
            public const string Mongo = "MongoDB";
            public const string ExternalConfigurationSource = "ExternalConfigurationSource";
            public const string Serilog = "Serilog";
        }

    }
}
