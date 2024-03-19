namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions
{
    public static class CorsServiceExtension
    {
        public static IServiceCollection AddCorsService(this IServiceCollection services, Action<CorsServiceOptions>? options = null)
        {
            var corsServiceOptions = new CorsServiceOptions();
            options?.Invoke(corsServiceOptions);
            
            services.AddCors(options =>
            {
                if (corsServiceOptions.Environment.IsDevelopment())
                {
                    options.AddPolicy("DevCorsPolicy",
                        builder => builder
                            .WithMethods(corsServiceOptions.AllowedMethods)
                            .SetIsOriginAllowed((host) => true)
                            .AllowAnyHeader()
                            .WithExposedHeaders(corsServiceOptions.AllowedExposedHeaders)
                            .AllowAnyMethod()
                            .AllowCredentials()
                            .SetPreflightMaxAge(TimeSpan.FromSeconds(corsServiceOptions.MaxAge))
                    );
                }
                else
                { 
                    options.AddPolicy(corsServiceOptions.CorsPolicyName,
                        builder => builder
                            .WithMethods(corsServiceOptions.AllowedMethods)
                            .WithOrigins(corsServiceOptions.AllowedOrigins)
                            .WithHeaders(corsServiceOptions.AllowedHeaders)
                            .WithExposedHeaders(corsServiceOptions.AllowedExposedHeaders)
                            .AllowCredentials()
                            .SetPreflightMaxAge(TimeSpan.FromSeconds(corsServiceOptions.MaxAge))
                    );                                                                                                               
                }
            });

            return services;
        }
    }

    public class CorsServiceOptions
    {
        public IWebHostEnvironment Environment { get; set; } = null!;
        public bool AllowCredentials { get; set; } = true;
        public string CorsPolicyName { get; set; } = "CorsPolicy";
        public string[] AllowedHeaders { get; set; } = new string[] { "Authorization, Content-Type, Accept, Origin, User-Agent, X-Requested-With" };
        public string[] AllowedExposedHeaders { get; set; } = new string[] { "X-Pagination" }   ;
        public string[] AllowedMethods { get; set; } = new string[] { "GET", "POST", "PUT", "DELETE", "OPTIONS" };
        public string[] AllowedOrigins { get; set; } = new string[] { "https://localhost:7007", "http://localhost:5005" };
        public int MaxAge { get; set; } = 3600;
    }
}
