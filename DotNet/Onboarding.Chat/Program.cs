using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp;
using Onboarding.Chat.Components;
using Onboarding.Chat.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();
var inContainer = builder.Configuration.GetValue<bool>("DOTNET_RUNNING_IN_CONTAINER");
builder.WebHost.UseUrls(inContainer ? "http://0.0.0.0:8080" : "http://127.0.0.1:8091");
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHealthChecks();
builder.Services.AddOptions<ChatSettings>()
    .Bind(builder.Configuration.GetSection("OnboardingChat"))
    .Validate(settings => ChatSettings.IsAllowedHttpUrl(settings.OllamaEndpoint, inContainer ? "host.docker.internal" : null)
        && ChatSettings.IsAllowedHttpUrl(settings.McpEndpoint, inContainer ? "onboarding-mcp" : null)
        && !string.IsNullOrWhiteSpace(settings.Model)
        && settings.ResponseTimeoutSeconds is >= 10 and <= 600,
        "Use loopback HTTP endpoints (or the designated Docker service hosts), a model name, and a response timeout between 10 and 600 seconds.")
    .ValidateOnStart();
builder.Services.AddSingleton<IChatClient>(services =>
{
    var settings = services.GetRequiredService<IOptions<ChatSettings>>().Value;
    IChatClient ollama = new OllamaApiClient(new Uri(settings.OllamaEndpoint), settings.Model);
    return ollama.AsBuilder().UseFunctionInvocation(configure: options => options.MaximumIterationsPerRequest = 6).Build();
});
builder.Services.AddScoped<OnboardingChatService>();

var app = builder.Build();
if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
app.UseAntiforgery();
app.MapHealthChecks("/health");
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
await app.RunAsync();
