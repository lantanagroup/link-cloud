namespace LantanaGroup.Link.DMRP.Config
{
    /// <summary>
    /// Settings that control the DMRP module hosted by the Tenant service.
    /// </summary>
    public class DmrpSettings
    {
        public const string ConfigSectionName = "DMRP";

        /// <summary>
        /// When false, none of the DMRP controllers, persistence or scheduling behavior is registered
        /// and the host continues to perform facility dQM reporting on its own.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// How to reach the DMRP API. Absent until an environment is pointed at one, which is why
        /// nothing here has a default: a base URL guessed wrong is worse than one left unset, since
        /// it turns a startup problem into a runtime call somewhere unexpected.
        /// </summary>
        public DmrpApiSettings Api { get; set; } = new();
    }

    /// <summary>
    /// Credentials and addresses for the DMRP API - the third-party service that says what a
    /// facility is enrolled to report.
    /// </summary>
    /// <remarks>
    /// The API is reached in two steps: a client-credentials token from <see cref="TokenUrl"/>,
    /// then the reporting-plan operations under <see cref="BaseUrl"/> carrying it as a bearer
    /// token. In the lower environments both are served by the mock.
    /// </remarks>
    public class DmrpApiSettings
    {
        /// <summary>
        /// Root the reporting-plan operations hang off. The operations sit at the root of the
        /// service - /msc and /ps/annual/mrp - because it impersonates nobody else's prefix.
        /// </summary>
        public string? BaseUrl { get; set; }

        /// <summary>The token endpoint the client-credentials grant is posted to.</summary>
        public string? TokenUrl { get; set; }

        public string? ClientId { get; set; }

        public string? ClientSecret { get; set; }

        /// <summary>
        /// Optional scope to request. Omitted from the token request when unset, which is what the
        /// mock expects; a real authorization server may require one.
        /// </summary>
        public string? Scope { get; set; }

        /// <summary>
        /// How long before a token's stated expiry it stops being reused, in seconds.
        /// </summary>
        /// <remarks>
        /// A token that expires in flight fails the call it was fetched for. Renewing slightly
        /// early costs one extra token request per lifetime and removes that whole class of
        /// failure, so the margin is here rather than in retry handling.
        /// </remarks>
        public int TokenExpiryMarginSeconds { get; set; } = 60;

        /// <summary>True when enough is configured to attempt a call.</summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(BaseUrl)
            && !string.IsNullOrWhiteSpace(TokenUrl)
            && !string.IsNullOrWhiteSpace(ClientId)
            && !string.IsNullOrWhiteSpace(ClientSecret);
    }
}
