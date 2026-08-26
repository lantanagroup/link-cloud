using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using LantanaGroup.Automation.Generation;

namespace Automation.UI.Services;

/// <summary>
/// Seeds the three system measure templates from embedded Automation bundles
/// and overwrites them on every startup so they stay in sync with the files.
/// ACH Daily/Monthly use the Validation NHSN 2.0.0-cibuild packages.
/// </summary>
public sealed class MeasureTemplateSeedService : IHostedService
{
    private readonly IMeasureTemplateStore _store;
    private readonly ILogger<MeasureTemplateSeedService> _logger;

    public MeasureTemplateSeedService(IMeasureTemplateStore store, ILogger<MeasureTemplateSeedService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var (id, family) in MeasureTemplateCatalog.SystemTemplates)
        {
            var bundleJson = ProfiledMeasureCatalog.ReadBundleJson(family);
            var parsed = MeasureBundleParser.Parse(bundleJson);
            var template = new MeasureTemplate
            {
                Id = id,
                Name = ProfiledMeasureCatalog.GetDisplayName(family),
                Description = "System measure definition used by Automation generation and prediction.",
                IsSystem = true,
                UpdatedAt = DateTimeOffset.UtcNow,
                GenerationFamily = family,
                BundleJson = bundleJson
            };
            MeasureBundleParser.ApplyMetadata(template, parsed);
            template.Name = ProfiledMeasureCatalog.GetDisplayName(family);

            await _store.UpsertAsync(template, cancellationToken);
            _logger.LogInformation(
                "Seeded system measure template {Name} ({Id}) version={Version}",
                template.Name, template.Id, template.Version ?? "(none)");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
