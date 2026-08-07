using Automation.UI.Models;
using LantanaGroup.Automation.Generation;
using MongoDB.Driver;

namespace Automation.UI.Services.Persistence;

/// <summary>
/// Resolves externally stored imported-bundle payloads for one execution without
/// exposing raw JSON through scenario, request, or snapshot models.
/// </summary>
public sealed class ImportedBundleExecutionResolver
{
    private readonly IMongoCollection<ImportedBundleDocument> _bundles;
    private readonly IImportedBundleContentStore _contentStore;

    public ImportedBundleExecutionResolver(IMongoDatabase database, IImportedBundleContentStore contentStore)
    {
        _bundles = database.GetCollection<ImportedBundleDocument>("automation_imported_bundles");
        _contentStore = contentStore;
    }

    public async Task<List<ImportedPatientInput>> ResolveAsync(
        IReadOnlyList<ImportedPatientInput> inputs,
        CancellationToken ct = default)
    {
        var bundleIds = inputs
            .Where(input => input.UploadedBundleId.HasValue)
            .Select(input => input.UploadedBundleId!.Value)
            .Distinct()
            .ToList();

        var documents = bundleIds.Count == 0
            ? []
            : await _bundles.Find(Builders<ImportedBundleDocument>.Filter.In(bundle => bundle.Id, bundleIds)).ToListAsync(ct);
        var documentsById = documents.ToDictionary(document => document.Id);
        var contentById = new Dictionary<Guid, string>();

        foreach (var bundleId in bundleIds)
        {
            if (!documentsById.TryGetValue(bundleId, out var document))
                throw new InvalidOperationException($"Imported bundle '{bundleId}' was not found.");

            var content = await _contentStore.ReadAsync(document, ct);
            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException($"Imported bundle '{bundleId}' content is unavailable.");

            contentById[bundleId] = content;
        }

        var unresolvedInput = inputs.FirstOrDefault(input => !input.UploadedBundleId.HasValue);
        if (unresolvedInput != null)
            throw new InvalidOperationException($"Imported bundle '{unresolvedInput.FileName ?? unresolvedInput.PatientId}' has no external content reference.");

        return inputs.Select(input => new ImportedPatientInput
        {
            Source = input.Source,
            PatientId = input.PatientId,
            FileName = input.FileName,
            UploadedBundleId = input.UploadedBundleId,
            BundleJson = input.UploadedBundleId is Guid bundleId && contentById.TryGetValue(bundleId, out var content)
                ? content
                : throw new InvalidOperationException($"Imported bundle '{input.FileName ?? input.PatientId}' content is unavailable."),
            AutoDetect = input.AutoDetect,
            MeasureEligibilities = new Dictionary<ProfiledMeasureType, MeasureEligibility>(input.MeasureEligibilities ?? []),
            DetectedClinicalScenarioId = input.DetectedClinicalScenarioId
        }).ToList();
    }
}
