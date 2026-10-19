using LantanaGroup.Link.Onboarding.Mcp;

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
builder.Services.AddHttpClient<OnboardingOptionsService>(client => client.Timeout = TimeSpan.FromSeconds(5))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHttpClient<CreateFacilityService>(client => client.Timeout = TimeSpan.FromSeconds(5))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddMcpServer().WithHttpTransport().WithTools<OnboardingTools>();
builder.Services.AddHttpClient("EpicEndpointDirectory", client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
    // The Brands Bundle includes care-facility addresses and currently exceeds 90 MB.
    client.MaxResponseContentBufferSize = 256 * 1024 * 1024;
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = false,
    AutomaticDecompression = System.Net.DecompressionMethods.All
});
builder.Services.AddSingleton(provider => new EpicEndpointDirectory(
    provider.GetRequiredService<IHttpClientFactory>().CreateClient("EpicEndpointDirectory"),
    provider.GetRequiredService<ILogger<EpicEndpointDirectory>>()));
builder.Services.AddHostedService(provider => provider.GetRequiredService<EpicEndpointDirectory>());
// Allow the validation endpoint's 30-second metadata probe to finish.
builder.Services.AddHttpClient<DataAcquisitionConfigurationService>(client => client.Timeout = TimeSpan.FromSeconds(35))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
var app = builder.Build();
app.MapHealthChecks("/health");
app.MapMcp("/mcp");
await app.RunAsync();

static bool IsLocalApiUrl(string value, string? containerHost) =>
    Uri.TryCreate(value, UriKind.Absolute, out var uri)
    && (uri.IsLoopback || string.Equals(uri.Host, containerHost, StringComparison.OrdinalIgnoreCase))
    && uri.Scheme == "http" && uri.AbsolutePath.EndsWith('/') && string.IsNullOrEmpty(uri.UserInfo);
