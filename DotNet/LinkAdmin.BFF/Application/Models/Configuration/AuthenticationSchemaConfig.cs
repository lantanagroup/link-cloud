namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration
{
    /// <summary>
    /// Authentication schema configuration
    /// </summary>
    public class AuthenticationSchemaConfig
    {
        /// <summary>
        /// The default authentication scheme
        /// </summary>
        public string DefaultScheme { get; set; } = null!;

        /// <summary>
        /// The default authentication challenge scheme
        /// </summary>
        public string DefaultChallengeScheme { get; set; } = null!;

        /// <summary>
        /// Whether anonymous access is enabled
        /// </summary>
        public bool EnableAnonymousAccess { get; set; } = false;
    }
}
