namespace LantanaGroup.Link.Tenant.Config
{
    public static class TenantConstants
    {
        public const string ServiceName = "Tenant";

        public static class AppSettingsSectionNames
        {
            public const string ServiceInformation = "ServiceInformation";
            public const string ExternalConfigurationSource = "ExternalConfigurationSource";
            public const string MeasureConfig = "MeasureConfig";
            public const string Serilog = "Serilog";
            public const string DatabaseProvider = "DatabaseProvider";
        }

        public static class TenantLoggingIds
        {
            public const int HealthCheck = 10010;
        }

        public static class Scheduler
        {
            public const string JobName = "JobName";

            public const string JobGroup = "JobGroup";

            public const string JobTrigger = "JobTrigger";

            public const string Facility = "Facility";

            public const string Frequency = "Frequency";

            public const string StartDate = "StartDate";

            public const string EndDate = "EndDate";

        }

    }
}
