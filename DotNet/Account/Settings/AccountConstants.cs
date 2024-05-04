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

        public static class AccountLoggingIds
        {
            public const int ApiRegistered = 1000;
            public const int UserCreated = 1001;
            public const int UserCreationException = 1002;
            public const int UserAddedToRole = 1003;
            public const int UserRemovedFromRole = 1004;
            public const int UserRoleAssignmentException = 1005;
            public const int UserRoleRemovalException = 1006;
        }
    }
}
