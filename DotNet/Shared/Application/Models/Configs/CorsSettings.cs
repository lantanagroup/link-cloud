using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LantanaGroup.Link.Shared.Application.Models.Configs
{
    /// <summary>
    /// CORS configuration options
    /// </summary>
    public class CorsSettings
    {
        /// <summary>
        /// Whether to enable CORS
        /// </summary>
        public bool EnableCors { get; set; } = true;

        /// <summary>
        /// Default CORS policy name
        /// </summary>
        public const string DefaultCorsPolicyName = "LinkCorsPolicy";

        /// <summary>
        /// Whether to allow all headers
        /// </summary>
        public bool AllowAllHeaders { get; set; } = false;

        /// <summary>
        /// Default allowed headers
        /// </summary>
        public string[] DefaultAllowedHeaders { get; } = ["Authorization", "Content-Type", "Accept", "Origin", "Access-Control-Allow-Origin", "User-Agent", "X-Requested-With"];

        /// <summary>
        /// Whether to allow all methods
        /// </summary>
        public bool AllowAllMethods { get; set; } = false;

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
        /// Whether to allow all origins
        /// </summary>
        public bool AllowAllOrigins { get; set; } = false;

        /// <summary>
        /// The allowed origins
        /// </summary>
        public string[]? AllowedOrigins { get; set; }

        /// <summary>
        /// The maximum age of the preflight request
        /// </summary>
        public int MaxAge { get; set; } = 600;
    }
}
