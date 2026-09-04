using Automation.UI.Services.Persistence;
using LantanaGroup.Automation.Generation;

namespace Automation.UI.Services;

public sealed class GenerationCatalogSeedService(
    IGenerationCatalogStore store,
    ILogger<GenerationCatalogSeedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var seed = GenerationCatalogSeed.FromHardcoded();
        await store.MergeAsync(seed, cancellationToken);
        logger.LogInformation("Seeded {Count} generation catalog rows from story-pack tables.", seed.Count);

        try
        {
            var imported = MeasureValueSetCatalogImporter.ImportAllEmbeddedMeasures();
            await store.MergeAsync(imported.Items, cancellationToken);
            EncounterIpClassification.RegisterDiabetesMedicationCodes(imported.DiabetesMedicationCodes);
            logger.LogInformation(
                "Imported {Count} generation catalog rows from measure value sets ({Diabetes} diabetes medication codes).",
                imported.Items.Count,
                imported.DiabetesMedicationCodes.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Measure value-set catalog import failed; pickers still have story-pack codes.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
