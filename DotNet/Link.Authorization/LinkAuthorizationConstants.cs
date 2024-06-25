namespace Link.Authorization.Infrastructure
{
    public class LinkAuthorizationConstants
    {

        public static class LinkSystemClaims
        {
            public const string Subject = "sub";
            public const string Email = "email";
            public const string Role = "roles";
            public const string Facility = "facilities";
            public const string LinkPermissions = "permissions";
        }

        public static class LinkUserClaims
        {
            public const string LinkSystemAccount = "SystemAccount";
            public const string LinkAdministartor = "LinkAdministrator";
            public const string LinkTenantAdministrator = "CanAdministerAllTenants";
            public const string FacilityAccess = "FacilityAccess";
        }

        public static class AuthenticationSchemas
        {
            public const string LinkBearerToken = "link_bearer_token";
        }

        public static class LinkBearerService
        {
            public const string LinkBearerIssuer = "LinkServiceAdmin";
            public const string LinkBearerAudience = "LinkServices";
            public const string LinkBearerKeyName = "link-bearer-key";
            public const string AuthenticatedUserPolicyName = "AuthenticatedUser";
        }
    }
}
