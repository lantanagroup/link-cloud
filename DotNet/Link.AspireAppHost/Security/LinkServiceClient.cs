using System.Net.Http.Headers;
using Link.AspireAppHost.Extensions;

namespace Link.AspireAppHost.Security;

/// <summary>
/// HTTP clients for the AppHost's own calls to Link services: Seed data and the facility and user commands.
/// </summary>
internal static class LinkServiceClient
{
    /// <summary>The services the AppHost's commands call, by the name the facility setup steps use.</summary>
    public static readonly IReadOnlyDictionary<string, int> ServicePorts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["account"] = Ports.Account,
        ["census"] = Ports.Census,
        ["data-acquisition"] = Ports.DataAcquisition,
        ["measureeval"] = Ports.MeasureEval,
        ["normalization"] = Ports.Normalization,
        ["querydispatch"] = Ports.QueryDispatch,
        ["report"] = Ports.Report,
        ["tenant"] = Ports.Tenant,
        ["validation"] = Ports.Validation
    };

    /// <summary>
    /// A client for the service on <paramref name="port"/>, carrying a Link system token. Under anonymous access
    /// the service ignores it; when the services require authentication (Auth:Mode Keycloak) it lets the call through.
    /// </summary>
    public static HttpClient Create(int port, TimeSpan? timeout = null)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(DotNetServiceExtensions.Url(port) + "/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", LinkSystemToken.Create());
        return client;
    }
}
