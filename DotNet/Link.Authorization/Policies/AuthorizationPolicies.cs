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

        public static AuthorizationPolicy CanViewLogs()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkPermissions.CanViewLogs)])
                .Build();
        }

        public static AuthorizationPolicy CanViewNotifications()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkPermissions.CanViewNotifications)])
                .Build();
        }

        public static AuthorizationPolicy CanViewTenantConfigurations()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkPermissions.CanViewTenantConfigurations)])
                .Build();
        }

        public static AuthorizationPolicy CanEditTenantConfigurations()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkPermissions.CanEditTenantConfigurations)])
                .Build();
        }

        public static AuthorizationPolicy CanAdministerAllTenants()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkPermissions.CanAdministerAllTenants)])
                .Build();
        }

        public static AuthorizationPolicy CanViewResources()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkPermissions.CanViewResources)])
                .Build();
        }

        public static AuthorizationPolicy CanViewReports()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkPermissions.CanViewReports)])
                .Build();
        }

        public static AuthorizationPolicy CanGenerateReports()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkPermissions.CanGenerateReports)])
                .Build();
        }     
        
        public static AuthorizationPolicy CanGenerateEvents()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkPermissions.CanGenerateEvents)])
                .Build();
        }



        //!** DEPRECATED **!
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
        
    }
}