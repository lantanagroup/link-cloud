using Link.Authorization.Infrastructure;
using Link.Authorization.Infrastructure.Requirements;
using Microsoft.AspNetCore.Authorization;

namespace Link.Authorization
{
    public static class AuthorizationPolicies
    {
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