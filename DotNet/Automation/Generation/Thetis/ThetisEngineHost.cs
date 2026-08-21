using Microsoft.Extensions.DependencyInjection;
using Thetis.Generation.Infrastructure;

namespace LantanaGroup.Automation.Generation.Thetis;

/// <summary>
/// Process-wide Engine composition for Automation. No Postgres. Seeded from
/// <see cref="NhsnRegistrySeed"/>. Thetis.Web never uses this host.
/// </summary>
internal static class ThetisEngineHost
{
    private static readonly Lazy<ServiceProvider> Provider = new(Build);

    public static IServiceProvider Services => Provider.Value;

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddThetisGeneration(o =>
        {
            o.SeedConditions = NhsnRegistrySeed.Conditions;
            o.SeedMedications = NhsnRegistrySeed.Medications;
        });
        return services.BuildServiceProvider();
    }
}
