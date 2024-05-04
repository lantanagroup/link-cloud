﻿namespace LantanaGroup.Link.Account.Settings
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
            public const int UpdateUser = 1007;
            public const int UpdateUserException = 1008;
            public const int DeactivateUser = 1009;
            public const int DeactivateUserException = 1010;
            public const int ActivateUser = 1011;
            public const int ActivateUserException = 1012;
            public const int FindUser = 1013;
            public const int FindUserException = 1014;
            public const int FindUsers = 1015;
            public const int FindUsersException = 1016;
            public const int UserClaimAssignment = 1017;
            public const int UserClaimAssignmentException = 1018;
            public const int UserClaimRemoval = 1019;
            public const int UserClaimRemovalException = 1020;
            public const int DeleteUser = 1021;
            public const int DeleteUserException = 1022;
            public const int UserRecovery = 1023;
            public const int UserRecoveryException = 1024;

        }
    }
}
