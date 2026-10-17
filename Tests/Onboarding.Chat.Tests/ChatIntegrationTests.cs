using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Onboarding.Chat.Services;
using OllamaSharp;
using Xunit;

namespace Onboarding.Chat.Tests;

public sealed class ChatIntegrationTests
{
    [Theory]
    [InlineData("http://localhost:11434", null, true)]
    [InlineData("http://host.docker.internal:11434", null, false)]
    [InlineData("http://host.docker.internal:11434", "host.docker.internal", true)]
    [InlineData("http://onboarding-mcp:8080/mcp", "onboarding-mcp", true)]
    [InlineData("http://untrusted.example/mcp", "onboarding-mcp", false)]
    [InlineData("http://user:secret@onboarding-mcp:8080/mcp", "onboarding-mcp", false)]
    public void EndpointValidationAllowsOnlyLocalOrDesignatedContainerHost(string value, string? containerHost, bool expected)
        => Assert.Equal(expected, ChatSettings.IsAllowedHttpUrl(value, containerHost));

    [Fact]
    public async Task OllamaAdapterExecutesDiscoveredMcpToolSchema()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddMcpServer().WithHttpTransport().WithTools<TestTools>();
        await using var server = builder.Build();
        server.MapMcp("/mcp");
        var requests = 0;
        server.MapPost("/api/chat", async (HttpContext context) =>
        {
            using var request = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
            requests++;
            object responseMessage = requests == 1
                ? new { role = "assistant", content = "", tool_calls = new[] { new { function = new { name = "get_onboarding_options", arguments = new { environmentId = "local" } } } } }
                : new { role = "assistant", content = "Found test-vendor" };
            context.Response.ContentType = "application/x-ndjson";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { model = "test", message = responseMessage, done = true }) + "\n", context.RequestAborted);
        });
        await server.StartAsync();
        try
        {
            IChatClient ollama = new OllamaApiClient(new Uri(server.Urls.Single()), "test");
            using var client = ollama.AsBuilder().UseFunctionInvocation().Build();
            var service = new OnboardingChatService(client, Options.Create(new ChatSettings { McpEndpoint = server.Urls.Single() + "/mcp" }));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var updates = new List<ChatResponseUpdate>();
            await foreach (var update in service.ReplyAsync([new(ChatRole.User, "Options")], timeout.Token)) updates.Add(update);
            Assert.Equal(2, requests);
            Assert.Contains("Found test-vendor", string.Concat(updates.Select(u => u.Text)));
        }
        finally { await server.StopAsync(); }
    }

    [Fact]
    public async Task ModelToolCallExecutesOverMcpAndPreservesHistory()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddMcpServer().WithHttpTransport().WithTools<TestTools>();
        await using var server = builder.Build();
        server.MapMcp("/mcp");
        await server.StartAsync();
        try
        {
            using var model = new ScriptedModel();
            using var client = model.AsBuilder().UseFunctionInvocation().Build();
            var settings = Options.Create(new ChatSettings { McpEndpoint = server.Urls.Single() + "/mcp" });
            var service = new OnboardingChatService(client, settings);
            List<ChatMessage> history = [new(ChatRole.User, "Show local onboarding options")];
            List<ChatResponseUpdate> updates = [];
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await foreach (var update in service.ReplyAsync(history, timeout.Token)) updates.Add(update);
            history.AddMessages(updates);

            Assert.Equal(2, model.Requests.Count);
            Assert.All(model.ToolNames, names => Assert.Equal(["get_onboarding_options"], names));
            Assert.Contains(model.Requests[1].SelectMany(m => m.Contents), content =>
                content is FunctionResultContent result && result.Result?.ToString()?.Contains("test-vendor") == true);
            Assert.Contains("Available vendor: test-vendor", string.Concat(updates.Select(u => u.Text)));
            Assert.Contains(history.SelectMany(m => m.Contents), c => c is FunctionCallContent);
            Assert.Contains(history.SelectMany(m => m.Contents), c => c is FunctionResultContent);

            history.Add(new(ChatRole.User, "Which vendor did you find?"));
            await foreach (var _ in service.ReplyAsync(history, timeout.Token)) { }
            Assert.Contains(model.Requests[2].SelectMany(m => m.Contents), c => c is FunctionResultContent);
        }
        finally { await server.StopAsync(); }
    }

    [Fact]
    public async Task CancellationStopsConnectionBeforeCallingModel()
    {
        using var model = new ScriptedModel();
        var service = new OnboardingChatService(model, Options.Create(new ChatSettings()));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in service.ReplyAsync([new(ChatRole.User, "Options")], cancellation.Token)) { }
        });
        Assert.Empty(model.Requests);
    }

    public sealed class ScriptedModel : IChatClient
    {
        public List<ChatMessage[]> Requests { get; } = [];
        public List<string[]> ToolNames { get; } = [];

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(messages.ToArray());
            ToolNames.Add(options?.Tools?.OfType<AIFunction>().Select(t => t.Name).ToArray() ?? []);
            await Task.Yield();
            yield return new ChatResponseUpdate
            {
                Role = ChatRole.Assistant,
                MessageId = $"message-{Requests.Count}",
                Contents = Requests.Count == 1
                    ? [new FunctionCallContent("test-call", "get_onboarding_options", new Dictionary<string, object?> { ["environmentId"] = "local" })]
                    : [new TextContent("Available vendor: test-vendor")]
            };
        }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    [McpServerToolType]
    public sealed class TestTools
    {
        [McpServerTool(Name = "get_onboarding_options"), Description("Return test onboarding options")]
        public static string GetOptions(string? environmentId = null) => "test-vendor";

        [McpServerTool(Name = "delete_facility"), Description("Must never be offered to this POC model")]
        public static string DeleteFacility() => throw new InvalidOperationException("Forbidden tool executed");
    }
}
