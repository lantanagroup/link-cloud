namespace QueryDispatch.Application.Settings
{
    public static class QueryDispatchConstants
    {
        public const string ServiceName = "Query Dispatch Service";

        /// <summary>
        /// The type of event that the dispatch should be triggered after.
        /// </summary>
        public enum EventType
        {
            Discharge
        }

        /// <summary>
        /// The type of duration (Example: minutes, hours, days) that the dispatch should be triggered after the event.
        /// </summary>
        public enum DurationType
        {
            Seconds,
            Minutes,
            Hours,
            Days
        }

        public static class AppSettingsSectionNames
        {
            public const string ServiceInformation = "ServiceInformation";
            public const string Telemetry = "TelemetryConfig";
            public const string TenantApiSettings = "TenantApiSettings";
            public const string ExternalConfigurationSource = "ExternalConfigurationSource";
        }

        public static class QueryDispatchLoggingIds
        {
            public const int HealthCheck = 10010;
        }
    }
}
