using Link.Authorization.Infrastructure.Requirements;
using Link.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Link.Authorization.Requirements.PermissiveAccessRequirement;

namespace Link.Authorization.Policies
{
    public static class AuthorizationPolicies
    {
        public static AuthorizationPolicy PermissiveAccess()
        {
            return new AuthorizationPolicyBuilder()                
                .AddRequirements(new PermissiveAccessRequirement())
                .Build();
        }

        public static AuthorizationPolicy LinkAdminAccess()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireRole(LinkAuthorizationConstants.LinkUserClaims.LinkAdministartor)
                .Build();
        }

        public static AuthorizationPolicy FacilityAccess()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new FacilityRequirement())
                .Build();
        }

        public static AuthorizationPolicy CanViewAuditLogs()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireRole([LinkAuthorizationConstants.LinkUserClaims.LinkAdministartor])
                .Build();
        }

        public static AuthorizationPolicy CanCreateNotifiactionConfigurations()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireRole([LinkAuthorizationConstants.LinkUserClaims.LinkAdministartor])
                .Build();
        }

        public static AuthorizationPolicy CanUpdateNotifiactionConfigurations()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireRole([LinkAuthorizationConstants.LinkUserClaims.LinkAdministartor])
                .Build();
        }

        public static AuthorizationPolicy CanDeleteNotifiactionConfigurations()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireRole([LinkAuthorizationConstants.LinkUserClaims.LinkAdministartor])
                .Build();
        }
    }
}