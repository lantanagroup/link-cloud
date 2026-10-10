using LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Audit;
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

                    // Background callers send a system token plus the user who started the work.
                    // Log that pairing once per proxied call; header values are never trusted for access.
                    builderContext.AddRequestTransform(transformContext =>
                    {
                        var entry = InitiatedByAudit.Read(transformContext.HttpContext);
                        if (entry is { } e)
                        {
                            logger.Information(
                                "Proxied {Method} {Route} as {Principal}, started by {InitiatedById} ({InitiatedByName})",
                                transformContext.HttpContext.Request.Method,
                                builderContext.Route.RouteId,
                                e.Principal,
                                e.InitiatedById,
                                e.InitiatedByName ?? string.Empty);
                        }
                        return ValueTask.CompletedTask;
                    });

                    if (!enableAnonymous)
                    {
                        if (!string.IsNullOrEmpty(builderContext.Route.AuthorizationPolicy))
                        {
                            builderContext.AddRequestTransform(async transformContext =>
                            {
                                var tokenService = services.BuildServiceProvider().GetRequiredService<ICreateLinkBearerToken>();
                                var token = await tokenService.ExecuteAsync(transformContext.HttpContext.User, 2, transformContext.HttpContext.RequestAborted);
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
