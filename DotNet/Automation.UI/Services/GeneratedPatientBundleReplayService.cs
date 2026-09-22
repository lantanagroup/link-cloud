using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Models;
using Automation.UI.Services.Persistence;

namespace Automation.UI.Services;

public sealed record GeneratedPatientBundleReplayResult(
    bool Found,
    string? BundleJson,
    string FileName,
    string? Error,
    int? RunCacheVersion,
    int? LatestCacheVersion,
    bool GenerationChanged);

/// <summary>
/// Replays a patient's generated FHIR from the ABS template cache (the same
/// templates used during the run), substituting this run's resource-ID tag.
/// Does not persist a per-run copy of the bundle.
/// </summary>
public sealed class GeneratedPatientBundleReplayService(
    IGeneratedPatientTemplateCache templateCache,
    IGeneratedTemplateCacheVersionLookup versionStore)
{
    public async Task<GeneratedPatientBundleReplayResult> ReplayAsync(
        AutomationRunSummary run,
        GenerationManifestSnapshot? manifest,
        string patientId,
        CancellationToken cancellationToken = default)
    {
        var fileName = FileNameFor(patientId);
        var latest = await versionStore.GetLatestAsync(run.GeneratedTemplateCacheScenarioKey, cancellationToken);
        var generationChanged = latest != null
            && run.GeneratedTemplateCacheVersionNumber is int runVersion
            && latest.VersionNumber > runVersion;

        if (manifest == null || !manifest.PatientIds.Contains(patientId, StringComparer.Ordinal))
        {
            return Unavailable("Patient is not on this run's generation manifest.", fileName, run, latest, generationChanged);
        }

        if (!manifest.TemplateCacheKeyByPatient.TryGetValue(patientId, out var templateKey)
            || string.IsNullOrWhiteSpace(templateKey))
        {
            return Unavailable(
                "This patient has no generation template to replay (imported patients are not generated, or the run predates template-key tracking). Re-run the scenario to enable download.",
                fileName,
                run,
                latest,
                generationChanged);
        }

        var template = await templateCache.GetAsync(templateKey, cancellationToken);
        if (template == null)
        {
            return Unavailable(
                "The generation template for this patient is no longer in cache. Re-run the scenario to rebuild it.",
                fileName,
                run,
                latest,
                generationChanged);
        }

        var runTag = FhirGenerationPipeline.TryInferRunTag([patientId])
            ?? FhirGenerationPipeline.TryInferRunTag(manifest.PatientIds);
        if (string.IsNullOrWhiteSpace(runTag))
        {
            return Unavailable(
                "Could not determine this run's generation tag from the patient IDs.",
                fileName,
                run,
                latest,
                generationChanged);
        }

        var json = FhirGenerationPipeline.MaterializeTemplateCollection(template, runTag);
        return new GeneratedPatientBundleReplayResult(
            Found: true,
            BundleJson: json,
            FileName: fileName,
            Error: null,
            RunCacheVersion: run.GeneratedTemplateCacheVersionNumber,
            LatestCacheVersion: latest?.VersionNumber,
            GenerationChanged: generationChanged);
    }

    public static string FileNameFor(string patientId)
    {
        var safe = string.Create(patientId.Length, patientId, static (span, id) =>
        {
            for (var i = 0; i < id.Length; i++)
            {
                var ch = id[i];
                span[i] = char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-';
            }
        });

        if (string.IsNullOrWhiteSpace(safe))
            safe = "patient";

        return $"generated-bundle-{safe}.json";
    }

    private static GeneratedPatientBundleReplayResult Unavailable(
        string error,
        string fileName,
        AutomationRunSummary run,
        GeneratedTemplateCacheVersionBinding? latest,
        bool generationChanged)
        => new(
            Found: false,
            BundleJson: null,
            FileName: fileName,
            Error: error,
            RunCacheVersion: run.GeneratedTemplateCacheVersionNumber,
            LatestCacheVersion: latest?.VersionNumber,
            GenerationChanged: generationChanged);
}
