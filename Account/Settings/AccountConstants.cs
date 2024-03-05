namespace LantanaGroup.Link.Account.Settings
{
    public static class AccountConstants
    {
        public const string ServiceName = "Account Service";

        public static class AppSettingsSectionNames
        {
            public const string ServiceInformation = "ServiceInformation";
            public const string Kafka = "KafkaConnection";
            public const string Postgres = "Postgres";
            public const string Telemetry = "TelemetryConfig";
            public const string TenantApiSettings = "TenantApiSettings";
        }
    }
}
