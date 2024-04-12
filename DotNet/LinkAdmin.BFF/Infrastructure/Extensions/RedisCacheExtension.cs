using StackExchange.Redis;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions
{
    public static class RedisCacheExtension
    {
        public static IServiceCollection AddRedisCache(this IServiceCollection services, Action<RedisCacheOptions> options)
        {
            var redisCacheOptions = new RedisCacheOptions();
            options(redisCacheOptions);

            services.AddStackExchangeRedisCache(options =>
            {                
                options.ConfigurationOptions = new ConfigurationOptions
                {
                    EndPoints = { redisCacheOptions.ConnectionString },
                };
                
                if(!string.IsNullOrEmpty(redisCacheOptions.InstanceName))
                {
                    options.InstanceName = redisCacheOptions.InstanceName;
                }              

                if (!string.IsNullOrEmpty(redisCacheOptions.Password))
                {
                    options.ConfigurationOptions.Password = redisCacheOptions.Password;
                }

            });

            return services;
        }

        public class RedisCacheOptions
        {
            public IWebHostEnvironment Environment { get; set; } = null!;
            public string? InstanceName { get; set; }
            public string ConnectionString { get; set; } = null!;
            public string? Password { get; set; }
        }
    }
}
