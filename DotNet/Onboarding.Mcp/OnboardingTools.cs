using System.ComponentModel;
using ModelContextProtocol.Server;

namespace LantanaGroup.Link.Onboarding.Mcp;

[McpServerToolType]
public sealed class OnboardingTools(OnboardingOptionsService service)
{
    [McpServerTool(Name = "get_onboarding_options", ReadOnly = true, Destructive = false, UseStructuredContent = true)]
    [Description("List locally configured Link Cloud onboarding environments and connection/query-plan profiles, live Tenant vendor/version choices, and live measure definitions from MeasureEval. No changes are made. Unavailable sources are explicit; only profiles are operator-maintained catalogs.")]
    public Task<OnboardingOptionsResult> GetOnboardingOptions(
        [Description("Optional environment ID returned by this tool. Omit to list all configured local environments.")]
        string? environmentId = null,
        CancellationToken cancellationToken = default) => service.GetAsync(environmentId, cancellationToken);
}
