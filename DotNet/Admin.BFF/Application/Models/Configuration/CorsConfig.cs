namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration
{
    /// <summary>
    /// CORS configuration options
    /// </summary>
    public class CorsConfig
    {
        /// <summary>
        /// Default CORS policy name
        /// </summary>
        public const string DefaultCorsPolicyName = "LinkAdminCorsPolicy";

        /// <summary>
        /// Default allowed headers
        /// </summary>
        public string[] DefaultAllowedHeaders { get; } = ["Authorization", "Content-Type", "Accept", "Origin", "Access-Control-Allow-Origin", "User-Agent", "X-Requested-With"];

        /// <summary>
        /// Default allowed methods
        /// </summary>
        public string[] DefaultAllowedMethods { get; } = ["GET", "POST", "PUT", "DELETE", "OPTIONS"];

        /// <summary>
        /// Default allowed exposed headers
        /// </summary>
        public string[] DefaultAllowedExposedHeaders { get; } = ["X-Pagination"];

        /// <summary>
        /// Whether to allow credentials
        /// </summary>
        public bool AllowCredentials { get; set; } = false;

        /// <summary>
        /// The name of the CORS policy
        /// </summary>
        public string? PolicyName { get; set; }

        /// <summary>
        /// The allowed headers
        /// </summary>
        public string[]? AllowedHeaders { get; set; }

        /// <summary>
        /// The allowed exposed headers
        /// </summary>
        public string[]? AllowedExposedHeaders { get; set; }

        /// <summary>
        /// The allowed HTTP methods
        /// </summary>
        public string[]? AllowedMethods { get; set; }

        /// <summary>
        /// The allowed origins
        /// </summary>
        public string[]? AllowedOrigins { get; set; }
        
        /// <summary>
        /// Allow all origins (ignore "AllowedOrigins")
        /// </summary>
        public bool AllowAllOrigins { get; set; } = false;

        /// <summary>
        /// The maximum age of the preflight request
        /// </summary>
        public int MaxAge { get; set; } = 600;
    }
}
