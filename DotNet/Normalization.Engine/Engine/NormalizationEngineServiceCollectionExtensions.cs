using LantanaGroup.Link.Normalization.Application.Services.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace LantanaGroup.Link.Normalization.Engine;

public static class NormalizationEngineServiceCollectionExtensions
{
    public static IServiceCollection AddNormalizationEngine(this IServiceCollection services)
    {
        services.AddSingleton<CopyPropertyOperationService>();
        services.AddSingleton<CodeMapOperationService>();
        services.AddSingleton<ConditionalTransformOperationService>();
        services.AddSingleton<CopyLocationOperationService>();
        services.AddSingleton<CopyLocationAliasToTypeIterativelyOperationService>();
        services.AddSingleton<RemoveExtensionsOperationService>();
        services.AddSingleton<NormalizationEngine>();
        return services;
    }
}
