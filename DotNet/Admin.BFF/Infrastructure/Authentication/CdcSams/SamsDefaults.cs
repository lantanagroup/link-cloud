namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Authentication.CdcSams
{
    public static class SamsDefaults
    {
        /// <summary>
        /// The default scheme used for CDC SAMS authentication
        /// </summary>
        public const string AuthenticationScheme = "CDC-SAMS";

        /// <summary>
        /// The display name used for CDC SAMS authentication
        /// </summary>
        public static readonly string DisplayName = "CDC-SAMS";

        /// <summary>
        /// The default endpoint used to perform CDC SAMS authentication.
        /// </summary>
        public static readonly string AuthorizationEndpoint = "";

        /// <summary>
        /// The default endpoint used to retrieve the token.
        /// </summary>
        public static readonly string TokenEndpoint = "";

        /// <summary>
        /// The default endpoint used to to validate the token.
        /// </summary>
        public static readonly string IntrospectionEndpoint = "";

        /// <summary>
        /// The default endpoint used to retrieve user information.
        /// </summary>
        public static readonly string UserInformationEndpoint = "";
    }
}
