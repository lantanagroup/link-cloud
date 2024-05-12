using Link.Authorization.Infrastructure.Requirements;
using Link.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Link.Authorization.Requirements.PermissiveAccessRequirement;
using Link.Authorization.Permissions;

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
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkPermissions.CanViewLogs)])
                .Build();
        }

        public static AuthorizationPolicy CanCreateNotifiactionConfigurations()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkPermissions.CanEditTenantConfigurations)])
                .Build();
        }

        public static AuthorizationPolicy CanUpdateNotifiactionConfigurations()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkPermissions.CanEditTenantConfigurations)])
                .Build();
        }

        public static AuthorizationPolicy CanDeleteNotifiactionConfigurations()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkPermissions.CanEditTenantConfigurations)])
                .Build();
        }

        public static AuthorizationPolicy CanViewAccounts()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkPermissions.CanViewAccounts)])
                .Build();
        }

        public static AuthorizationPolicy CanAdministerAccounts()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkPermissions.CanAdministerAccounts)])
                .Build();
        }
    }
}