namespace LantanaGroup.Link.Account.Settings
{
    public static class AccountConstants
    {
        public const string ServiceName = "Account";

        public static class AppSettingsSectionNames
        {
            public const string DatabaseProvider = "DatabaseProvider";
            public const string UserManagement = "UserManagement";
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
            public const int SearchUsers = 1025;
            public const int SearchUsersException = 1026;
            public const int UserLocked = 1027;
            public const int UserLockedException = 1028;
        
            public const int RoleCreated = 1100;
            public const int RoleCreationException = 1101;
            public const int RoleDeleted = 1102;
            public const int RoleDeletionException = 1103;
            public const int RoleUpdated = 1104;
            public const int RoleUpdateException = 1105;
            public const int FindRole = 1106;
            public const int FindRoleException = 1107;
            public const int FindRoles = 1108;
            public const int FindRolesException = 1109;
            public const int RoleClaimAssignment = 1110;
            public const int RoleClaimAssignmentException = 1111;
            public const int RoleClaimRemoval = 1112;
            public const int RoleClaimRemovalException = 1113;
            public const int RoleNotFound = 1114;

            public const int AuditEventCreated = 1200;
            public const int AuditEventCreationException = 1201;
            public const int CacheException = 1202;
        }
    }
}
