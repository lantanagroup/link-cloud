using LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security;
using Yarp.ReverseProxy.Transforms;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions
{
    public static class YarpProxyExtensioncs
    {
        public static IServiceCollection AddYarpProxy(this IServiceCollection services, IConfiguration configuration, Action<ProxyOptions>? options = null)
        {
            var proxyOptions = new ProxyOptions();
            options?.Invoke(proxyOptions);

            services.AddReverseProxy()
                .LoadFromConfig(configuration.GetRequiredSection("ReverseProxy"))
                .AddTransforms(builderContext => {

                    if(!proxyOptions.Environment.IsDevelopment() || !configuration.GetValue<bool>("Authentication:EnableAnonymousAccess"))
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
