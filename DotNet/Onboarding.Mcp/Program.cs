using System.Text.Json;
using System.Text.Json.Serialization;
using Flurl.Http;
using Flurl.Http.Configuration;
using LantanaGroup.Link.Onboarding.Mcp;
using LantanaGroup.Link.Sdk.Clients;
using Microsoft.Extensions.Options;

// Resolve appsettings beside the executable even when launched from the repository root.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
// Avoid Windows Event Log registration/permissions for a local console process.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
// Docker publishes the container port only on the host's loopback interface.
var inContainer = builder.Configuration.GetValue<bool>("DOTNET_RUNNING_IN_CONTAINER");
builder.WebHost.UseUrls(inContainer ? "http://0.0.0.0:8080" : "http://127.0.0.1:8090");
builder.Services.AddHealthChecks();
builder.Services.AddOptions<OnboardingOptions>()
    .Bind(builder.Configuration.GetSection("Onboarding"))
    .Validate(_ => new[] { builder.Configuration.GetSection("Onboarding:ConnectionProfiles"), builder.Configuration.GetSection("Onboarding:QueryPlanProfiles") }
        .All(profiles => profiles.Value is null && profiles.GetChildren().All(profile =>
            profile.Value is null && !string.IsNullOrWhiteSpace(profile["Id"])
                && !string.IsNullOrWhiteSpace(profile["DisplayName"]))),
        "ConnectionProfiles and QueryPlanProfiles must be arrays of JSON objects with non-empty Id and DisplayName fields, not JSON-encoded strings.")
    .Validate(options => IsLocalApiUrl(options.TenantApiBaseUrl, inContainer ? "tenant" : null)
        && IsLocalApiUrl(options.MeasureEvalApiBaseUrl, inContainer ? "measureeval" : null)
        && IsLocalApiUrl(options.DataAcquisitionApiBaseUrl, inContainer ? "dataacq" : null),
        "Configure loopback HTTP (or designated Docker service) Tenant and MeasureEval API URLs ending in '/'.")
    .ValidateOnStart();
builder.Services.AddSingleton<IFacilityServiceClient>(services =>
{
    var options = services.GetRequiredService<IOptions<OnboardingOptions>>().Value;
    return new FacilityServiceClient(LinkClient(options.TenantApiBaseUrl, options.BearerToken, 5));
});
builder.Services.AddSingleton<IDataAcquisitionServiceClient>(services =>
{
    var options = services.GetRequiredService<IOptions<OnboardingOptions>>().Value;
    // Allow the validation endpoint's 30-second metadata probe to finish.
    return new DataAcquisitionServiceClient(LinkClient(options.DataAcquisitionApiBaseUrl,
        options.DataAcquisitionBearerToken, 35));
});
builder.Services.AddSingleton<CreateFacilityService>();
builder.Services.AddSingleton<TenantVendorService>();
builder.Services.AddSingleton<DataAcquisitionConfigurationService>();
builder.Services.AddMcpServer().WithHttpTransport().WithTools<OnboardingTools>();
builder.Services.AddSingleton<LuxeraEndpointApi>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient(LuxeraEndpointApi.ClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
    client.MaxResponseContentBufferSize = 2 * 1024 * 1024;
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
var app = builder.Build();
app.MapHealthChecks("/health");
app.MapMcp("/mcp");
await app.RunAsync();

static bool IsLocalApiUrl(string value, string? containerHost) =>
    Uri.TryCreate(value, UriKind.Absolute, out var uri)
    && (uri.IsLoopback || string.Equals(uri.Host, containerHost, StringComparison.OrdinalIgnoreCase))
    && uri.Scheme == "http" && uri.AbsolutePath.EndsWith('/') && string.IsNullOrEmpty(uri.UserInfo);

static IFlurlClient LinkClient(string baseUrl, string? bearerToken, int timeoutSeconds)
{
    var client = new FlurlClient(baseUrl);
    client.Settings.JsonSerializer = new DefaultJsonSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    });
    client.Settings.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
    client.Settings.Redirects.Enabled = false;
    if (!string.IsNullOrWhiteSpace(bearerToken))
        client.WithOAuthBearerToken(bearerToken);
    return client;
}
