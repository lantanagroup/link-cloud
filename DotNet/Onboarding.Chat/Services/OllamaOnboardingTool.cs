using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace Onboarding.Chat.Services;

// OllamaSharp 5.x expects a string "type" for tool properties. MCP correctly
// advertises the optional environmentId as ["string", "null"]. Advertise it to
// Ollama as an optional string; invocation still delegates to the real MCP tool.
internal sealed class OllamaOnboardingTool(AIFunction innerFunction) : DelegatingAIFunction(innerFunction)
{
    public override JsonElement JsonSchema { get; } = AdaptSchema(innerFunction.JsonSchema);

    private static JsonElement AdaptSchema(JsonElement schema)
    {
        var node = JsonNode.Parse(schema.GetRawText())!;
        if (node["properties"]?["environmentId"] is JsonObject environment
            && environment["type"] is JsonArray types
            && types.Count == 2
            && types.Any(t => t?.GetValue<string>() == "string")
            && types.Any(t => t?.GetValue<string>() == "null"))
            environment["type"] = "string";
        return JsonSerializer.SerializeToElement(node);
    }
}
