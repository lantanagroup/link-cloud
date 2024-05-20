using Link.Authorization.Infrastructure.Requirements;
using Link.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Link.Authorization.Requirements.PermissiveAccessRequirement;
using Link.Authorization.Permissions;

namespace Link.Authorization.Policies
{
    public static class AuthorizationPolicies
    {
        public static AuthorizationPolicy IsLinkAdmin()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkSystemPermissions.IsLinkAdmin)])
                .Build();
        }

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
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkSystemPermissions.CanViewAccounts)])
                .Build();
        }

        public static AuthorizationPolicy CanAdministerAccounts()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkSystemPermissions.CanAdministerAccounts)])
                .Build();
        }

        public static AuthorizationPolicy CanViewLogs()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkSystemPermissions.CanViewLogs)])
                .Build();
        }

        public static AuthorizationPolicy CanViewNotifications()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkSystemPermissions.CanViewNotifications)])
                .Build();
        }

        public static AuthorizationPolicy CanViewTenantConfigurations()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkSystemPermissions.CanViewTenantConfigurations)])
                .Build();
        }

        public static AuthorizationPolicy CanEditTenantConfigurations()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkSystemPermissions.CanEditTenantConfigurations)])
                .Build();
        }

        public static AuthorizationPolicy CanAdministerAllTenants()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkSystemPermissions.CanAdministerAllTenants)])
                .Build();
        }

        public static AuthorizationPolicy CanViewResources()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkSystemPermissions.CanViewResources)])
                .Build();
        }

        public static AuthorizationPolicy CanViewReports()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkSystemPermissions.CanViewReports)])
                .Build();
        }

        public static AuthorizationPolicy CanGenerateReports()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkSystemPermissions.CanGenerateReports)])
                .Build();
        }     
        
        public static AuthorizationPolicy CanGenerateEvents()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkSystemPermissions.CanGenerateEvents)])
                .Build();
        }



        //!** DEPRECATED **!
        public static AuthorizationPolicy CanViewAuditLogs()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkSystemPermissions.CanViewLogs)])
                .Build();
        }

        public static AuthorizationPolicy CanCreateNotifiactionConfigurations()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkSystemPermissions.CanEditTenantConfigurations)])
                .Build();
        }

        public static AuthorizationPolicy CanUpdateNotifiactionConfigurations()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkSystemPermissions.CanEditTenantConfigurations)])
                .Build();
        }

        public static AuthorizationPolicy CanDeleteNotifiactionConfigurations()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, [nameof(LinkSystemPermissions.CanEditTenantConfigurations)])
                .Build();
        }
        
    }
}