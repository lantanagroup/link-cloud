﻿namespace Link.Authorization.Infrastructure
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
        }
    }
}
