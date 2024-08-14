using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace LantanaGroup.Link.Shared.Application.Extensions.ExternalServices
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

                if (!string.IsNullOrEmpty(redisCacheOptions.InstanceName))
                {
                    options.InstanceName = redisCacheOptions.InstanceName;
                }

                if (!string.IsNullOrEmpty(redisCacheOptions.Password))
                {
                    options.ConfigurationOptions.Password = redisCacheOptions.Password;
                }

                if (redisCacheOptions.Timeout > 0)
                {
                    options.ConfigurationOptions.ConnectTimeout = (int)redisCacheOptions.Timeout;
                }
                

            });

            return services;
        }

        public class RedisCacheOptions : CacheSettings
        {
            public IWebHostEnvironment Environment { get; set; } = null!;
        }
    }
}
