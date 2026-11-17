using System.Security.Claims;
using Claim = System.Security.Claims.Claim;
using LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace IntegrationTests.AdminBFF;

/// <summary>
/// Hosts the real Admin BFF, with its shipped appsettings.json and its real authentication schemes, so tests
/// can see what a caller gets back. Only what would otherwise reach outside the process is overridden.
/// </summary>
public sealed class AdminBffApiFactory : WebApplicationFactory<AuthEndpoints>
{
    /// <summary>
    /// The cookie scheme Admin BFF signs users in with.
    /// </summary>
    public const string CookieScheme = "link_cookie";

    private readonly string _challengeScheme;
    private readonly bool _oauth2Enabled;

    /// <summary>
    /// Creates a host with <paramref name="challengeScheme"/> as its default challenge scheme and the OAuth2
    /// scheme on or off: <c>link_oauth2</c> with OAuth2 on as shipped in appsettings.json, or
    /// <c>link_cookie</c> with OAuth2 off as local compose sets them.
    /// </summary>
    public AdminBffApiFactory(string challengeScheme, bool oauth2Enabled)
    {
        _challengeScheme = challengeScheme;
        _oauth2Enabled = oauth2Enabled;
    }

    /// <summary>
    /// Creates a client that reports redirects instead of following them, over HTTPS so the cookie's
    /// secure policy holds.
    /// </summary>
    public HttpClient CreateNonRedirectingClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    /// <summary>
    /// Builds a <c>Cookie</c> header value for a user signed in through the cookie scheme with
    /// <paramref name="claims"/>, protected by the host's own data protection keys.
    /// </summary>
    public string CreateSignedInCookie(params Claim[] claims)
    {
        var options = Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieScheme);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieScheme));
        var ticket = new AuthenticationTicket(principal, CookieScheme);

        return $"{options.Cookie.Name}={options.TicketDataFormat.Protect(ticket)}";
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Program.cs reads its configuration while it registers services, before ConfigureAppConfiguration
        // callbacks are applied, so the overrides go in as settings, which it sees from the start.
        builder.UseEnvironment(Environments.Production);
        foreach (var (key, value) in Overrides())
        {
            builder.UseSetting(key, value);
        }
    }

    private Dictionary<string, string> Overrides()
    {
        var overrides = new Dictionary<string, string>
        {
            ["Authentication:EnableAnonymousAccess"] = "false",
            ["Authentication:DefaultChallengeScheme"] = _challengeScheme,
            ["Authentication:Schemas:Oauth2:Enabled"] = _oauth2Enabled ? "true" : "false",
            ["Authentication:Schemas:Oauth2:ClientId"] = "admin-bff-tests",
            ["Authentication:Schemas:Oauth2:ClientSecret"] = "admin-bff-tests",
            ["Authentication:Schemas:Oauth2:Endpoints:Authorization"] = "https://idp.test/authorize",
            ["Authentication:Schemas:Oauth2:Endpoints:Token"] = "https://idp.test/token",
            ["Authentication:Schemas:Oauth2:Endpoints:UserInformation"] = "https://idp.test/userinfo",
            ["KafkaConnection:BootstrapServers:0"] = "localhost:9092",
            ["Cache:Type"] = "InMemory",
            ["PipelineAbort:AllowInMemory"] = "true",
            ["MonitorBackendHealthChecks"] = "false",
            ["EnableIntegrationFeature"] = "false",
            ["ServiceRegistry:AccountServiceUrl"] = "http://localhost:1",
            ["ServiceRegistry:TenantService:TenantServiceUrl"] = "http://localhost:1"
        };

        // The shipped destinations are empty and filled in per environment. Requests in these tests are refused
        // before they are proxied, so any address will do. ServiceTests.csproj links the shipped file here.
        var shipped = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "Resources", "Admin.BFF", "appsettings.json"))
            .Build();
        foreach (var cluster in shipped.GetSection("ReverseProxy:Clusters").GetChildren())
        {
            foreach (var destination in cluster.GetSection("Destinations").GetChildren())
            {
                overrides[$"{destination.Path}:Address"] = "http://localhost:1";
            }
        }

        return overrides;
    }
}
