using LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Filters;
using Yarp.ReverseProxy.Transforms;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions
{
    public static class YarpProxyExtensioncs
    {
        public static IServiceCollection AddYarpProxy(this IServiceCollection services, IConfiguration configuration, Serilog.ILogger logger, Action<ProxyOptions>? options = null)
        {
            var proxyOptions = new ProxyOptions();
            options?.Invoke(proxyOptions);

            services.AddReverseProxy()
                .LoadFromConfig(configuration.GetRequiredSection("ReverseProxy"))
                .AddConfigFilter<YarpConfigFilter>()
                .AddTransforms(builderContext =>
                {
                    bool enableAnonymous = configuration.GetValue<bool>("Authentication:EnableAnonymousAccess");

                    if (proxyOptions.Environment.IsDevelopment() && enableAnonymous)
                        logger.Error("Anonymous access is enabled in development mode. This is a security risk.");
                    
                    if (!enableAnonymous)
                    {
                        if (!string.IsNullOrEmpty(builderContext.Route.AuthorizationPolicy))
                        {
                            builderContext.AddRequestTransform(async transformContext =>
                            {
                                var tokenService = services.BuildServiceProvider().GetRequiredService<ICreateLinkBearerToken>();
                                var token = await tokenService.ExecuteAsync(transformContext.HttpContext.User, 2);
                                transformContext.ProxyRequest.Headers.Remove("Authorization");
                                transformContext.ProxyRequest.Headers.Add("Authorization", $"Bearer {token}");
                            });
                        }
                    }                    

                });

            return services;
        }
    }

    public class ProxyOptions
    {
        public IWebHostEnvironment Environment { get; set; } = null!;
    }
}
