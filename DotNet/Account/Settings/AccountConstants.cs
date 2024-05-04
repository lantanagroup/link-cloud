namespace LantanaGroup.Link.Account.Settings
{
    public static class AccountConstants
    {
        public const string ServiceName = "Account";

        public static class AppSettingsSectionNames
        {
            public const string ServiceInformation = "ServiceInformation";
            public const string DatabaseProvider = "DatabaseProvider";
            public const string DatabaseConnectionString = "ConnectionStrings:DatabaseConnection";            
            public const string Postgres = "Postgres";
            public const string Telemetry = "Telemetry";
            public const string TenantApiSettings = "TenantApiSettings";
            public const string ExternalConfigurationSource = "ExternalConfigurationSource";
        }
    }
}
