using Automation.UI.Models;
using Automation.UI.Services.Persistence;

namespace Automation.UI.Services;

/// <summary>
/// Loads selected measure templates and attaches their FHIR bundles onto resolved run options.
/// Cosmos/Mongo I/O is Find-by-id only.
/// </summary>
public static class MeasureTemplateRunBinder
{
    public static async Task<ResolvedRunOptions> AttachBundlesAsync(
        ResolvedRunOptions options,
        IMeasureTemplateStore store,
        CancellationToken cancellationToken)
    {
        var ids = options.SelectedMeasureIds.Count > 0
            ? options.SelectedMeasureIds
            : MeasureTemplateCatalog.SystemIdsFor(options.SelectedMeasures);

        if (ids.Count == 0)
            return options;

        var templates = await store.GetByIdsAsync(ids, cancellationToken);
        var missing = ids.Except(templates.Select(t => t.Id)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Selected measure template(s) were not found: {string.Join(", ", missing)}");
        }

        var families = templates.Select(t => t.GenerationFamily).Distinct().ToList();
        var jsons = templates.Select(t => t.BundleJson).Where(j => !string.IsNullOrWhiteSpace(j)).ToList();

        return options with
        {
            SelectedMeasureIds = ids,
            SelectedMeasures = families.Count > 0 ? families : options.SelectedMeasures,
            MeasureBundleJsons = jsons
        };
    }
}
