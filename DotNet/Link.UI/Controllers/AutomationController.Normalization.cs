using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Automation.Link.Models;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

public sealed partial class AutomationController
{
    /// <summary>
    /// Normalization suite, organization map, patient source and execution evidence for one run.
    /// The run details page loads it on demand. Missing pieces come back as explicit states.
    /// </summary>
    [HttpGet("normalization-results")]
    public async Task<IActionResult> NormalizationResults(Guid id, CancellationToken cancellationToken)
    {
        if (EngineOff() is { } off)
            return off;

        var run = await _manager!.GetRunAsync(id, cancellationToken);
        if (run == null)
            return NotFound();

        var manifest = await _manager.GetGenerationManifestAsync(id, cancellationToken);
        NormalizationEvidenceSnapshot? evidence = null;
        if (_services.GetService<ISnapshotStore>() is { } snapshots)
        {
            try
            {
                evidence = (await snapshots.GetDomainAsync<NormalizationEvidenceSnapshot>(
                    id,
                    NormalizationEvidenceSnapshot.Domain,
                    cancellationToken))?.Data;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    "Normalization evidence could not be read for a run ({ExceptionType}).",
                    ex.GetType().Name);
            }
        }

        var config = RunConfigurationReader.Read(run.RunConfigurationJson);
        var suiteId = manifest?.AppliedNormalizationSuiteId ?? config.SuiteId;
        var hasRecordedSequences = evidence?.SuiteSequences is { Count: > 0 };
        NormalizationSuiteDefinition? suite = null;
        List<NormalizationOperationDefinition>? operations = null;
        List<NormalizationSequenceDefinition>? sequences = null;
        var suiteChangedAfterRun = false;
        if (suiteId.HasValue && !hasRecordedSequences && _services.GetService<INormalizationStore>() is { } normalizations)
        {
            suite = await normalizations.GetSuiteByIdAsync(suiteId.Value, cancellationToken);
            if (suite != null)
            {
                operations = await normalizations.GetAllOperationsAsync(cancellationToken);
                sequences = await normalizations.GetAllSequencesAsync(cancellationToken);
                suiteChangedAfterRun = NormalizationRunResultsBuilder.SuiteDefinitionChangedAfter(
                    suite,
                    operations,
                    sequences,
                    run.StartedAt ?? run.CreatedAt);
            }
        }

        var mapId = manifest?.AppliedOrganizationResourceMapTemplateId ?? config.OrganizationMapId;
        OrganizationResourceMapTemplate? organizationMap = null;
        if (mapId.HasValue && _services.GetService<IOrganizationResourceMapTemplateStore>() is { } maps)
            organizationMap = await maps.GetByIdAsync(mapId.Value, cancellationToken);

        var finished = run.Status is AutomationRunStatus.Succeeded
            or AutomationRunStatus.Failed
            or AutomationRunStatus.Cancelled;
        var results = NormalizationRunResultsBuilder.Build(new NormalizationRunResultsSource
        {
            RunConfigurationJson = run.RunConfigurationJson,
            RunFinished = finished,
            Manifest = manifest,
            Evidence = evidence,
            Suite = suite,
            SuiteOperations = operations,
            SuiteSequences = sequences,
            SuiteChangedAfterRun = suiteChangedAfterRun,
            OrganizationMap = organizationMap
        });
        return Json(results);
    }
}
