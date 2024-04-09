using Microsoft.AspNetCore.Authorization;

namespace Link.Authorization
{
    public static class AuthorizationPolicies
    {
        public static AuthorizationPolicy CanViewAuditLogs() 
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireRole("LinkAdministrator")
                .Build();
        }

        public static AuthorizationPolicy CanCreateNotifiactionConfigurations()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireRole("LinkAdministrator")
                .Build();
        }

        public static AuthorizationPolicy CanUpdateNotifiactionConfigurations()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireRole("LinkAdministrator")
                .Build();
        }

        public static AuthorizationPolicy CanDeleteNotifiactionConfigurations()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireRole("LinkAdministrator")
                .Build();
        }
    }
}