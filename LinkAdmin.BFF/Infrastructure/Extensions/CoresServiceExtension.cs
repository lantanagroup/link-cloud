namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions
{
    public static class CoresServiceExtension
    {
        public static IServiceCollection AddCorsService(this IServiceCollection services, IWebHostEnvironment env)
        {
            //TODO: Use env variable to control strictness of CORS policy
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                    builder => builder
                        .AllowAnyMethod()
                        .AllowCredentials()
                        .SetIsOriginAllowed((host) => true) //lock this down, allows all atm
                        .AllowAnyHeader());
            });

            return services;
        }
    }
}
