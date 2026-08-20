using System.Text.Json;
using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using Hl7.Fhir.Model;
using LantanaGroup.Automation;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Normalization.Engine;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Automation.UI.Services.ConfigurationGeneration;

public sealed class BundleConfigurationGenerationService(
    INormalizationStore normalizationStore,
    IOrganizationResourceMapTemplateStore ormStore,
    IImportedBundleContentStore bundleContentStore,
    IMongoDatabase database,
    IOptions<AutomationConfig> automationConfig,
    NormalizationEngine normalizationEngine)
{
    private readonly IMongoCollection<ImportedBundleDocument> _bundles =
        database.GetCollection<ImportedBundleDocument>("automation_imported_bundles");

    public async Task<BundleConfigurationProposal> AnalyzeAsync(
        AnalyzeBundleConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var sources = CollectSources(request);
        if (sources.Count == 0)
            throw new InvalidOperationException("Provide bundle JSON, an uploaded bundle id, or an existing FHIR patient id.");

        var resources = new List<Resource>();
        foreach (var source in sources)
            resources.AddRange(await LoadResourcesAsync(source, cancellationToken));

        if (resources.Count == 0)
            throw new InvalidOperationException("The FHIR bundle(s) contained no resources.");

        var fingerprint = UploadedBundleAnalyzer.Analyze(resources);
        var combinedWithPrior = request.PriorFingerprint != null;
        if (combinedWithPrior)
            fingerprint = UploadedBundleAnalyzer.Merge(request.PriorFingerprint!, fingerprint);

        var templates = await ormStore.GetAllAsync(cancellationToken);
        OrganizationResourceMapTemplate? refineOrm = null;
        if (request.RefineOrmId is { } ormId)
            refineOrm = templates.FirstOrDefault(t => t.Id == ormId);

        var ops = await normalizationStore.GetAllOperationsAsync(cancellationToken);
        var sequences = await normalizationStore.GetAllSequencesAsync(cancellationToken);
        var suites = await normalizationStore.GetAllSuitesAsync(cancellationToken);
        NormalizationSuiteDefinition? refineSuite = null;
        if (request.RefineSuiteId is { } suiteId)
            refineSuite = suites.FirstOrDefault(s => s.Id == suiteId);

        var orm = OrgResourceMapProposalBuilder.Build(fingerprint, templates, refineOrm);
        var normalization = NormalizationProposalBuilder.Build(fingerprint, ops, suites, sequences, refineSuite);
        await AddPostNormalizationPredictionNotesAsync(
            resources,
            ops,
            sequences,
            refineSuite,
            normalization,
            cancellationToken);

        var summary = new List<string>
        {
            $"{fingerprint.PatientCount} Patient, {fingerprint.LocationCount} Location, {fingerprint.ResourceCounts.Values.Sum()} total resource(s) across {sources.Count} source(s).",
            fingerprint.LocationIdentifiers.Count > 0
                ? $"{fingerprint.LocationIdentifiers.Count} distinct Location identifier(s) / {DistinctSystems(fingerprint)} identifier system(s)."
                : "No Location identifiers found.",
            fingerprint.Extensions.Count > 0
                ? $"{fingerprint.Extensions.Count} distinct extension(s) across resource types."
                : "No extensions found."
        };
        if (combinedWithPrior)
            summary.Insert(0, "Combined with previously analyzed patient data so the proposal covers every upload in this session.");

        return new BundleConfigurationProposal
        {
            Fingerprint = fingerprint,
            Orm = orm,
            Normalization = normalization,
            Summary = summary,
            CombinedWithPrior = combinedWithPrior,
            SourceCount = sources.Count,
            RefinedOrmId = refineOrm?.Id,
            RefinedSuiteId = refineSuite?.Id
        };
    }

    public async Task<OrganizationResourceMapTemplate> ApplyOrmAsync(
        ApplyGeneratedOrmRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Proposal.Conditions.Count == 0)
            throw new InvalidOperationException("The ORM proposal has no conditions to save.");

        OrganizationResourceMapTemplate model;
        if (request.UpdateExistingId is { } id)
        {
            model = await ormStore.GetByIdAsync(id, cancellationToken)
                    ?? throw new InvalidOperationException("The ORM to update was not found.");
            if (model.IsSystem)
            {
                model = new OrganizationResourceMapTemplate
                {
                    Id = Guid.NewGuid(),
                    Name = UniqueName(string.IsNullOrWhiteSpace(request.Proposal.SuggestedName)
                        ? $"{model.Name} extended"
                        : request.Proposal.SuggestedName),
                    Description = request.Proposal.SuggestedDescription ?? model.Description,
                    Conditions = request.Proposal.Conditions,
                    IsSystem = false,
                    IsDefault = false
                };
            }
            else
            {
                model.Conditions = request.Proposal.Conditions;
                if (!string.IsNullOrWhiteSpace(request.Proposal.SuggestedDescription))
                    model.Description = request.Proposal.SuggestedDescription;
                if (!string.IsNullOrWhiteSpace(request.Proposal.SuggestedName))
                    model.Name = request.Proposal.SuggestedName.Trim();
            }
        }
        else
        {
            model = new OrganizationResourceMapTemplate
            {
                Id = Guid.NewGuid(),
                Name = UniqueName(request.Proposal.SuggestedName),
                Description = request.Proposal.SuggestedDescription,
                Conditions = request.Proposal.Conditions,
                IsSystem = false,
                IsDefault = false
            };
        }

        model.UpdatedAt = DateTimeOffset.UtcNow;
        await ormStore.UpsertAsync(model, cancellationToken);
        return model;
    }

    public async Task<NormalizationSuiteDefinition> ApplyNormalizationAsync(
        ApplyGeneratedNormalizationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Proposal.Operations.Count == 0 && request.UpdateExistingSuiteId is null)
            throw new InvalidOperationException("The normalization proposal has no operations to save.");

        var sequences = await normalizationStore.GetAllSequencesAsync(cancellationToken);

        NormalizationSuiteDefinition? existingSuite = null;
        var clonedSystemSuite = false;
        HashSet<Guid> alreadyInSuite = [];
        if (request.UpdateExistingSuiteId is { } suiteId)
        {
            existingSuite = await normalizationStore.GetSuiteByIdAsync(suiteId, cancellationToken)
                            ?? throw new InvalidOperationException("The suite to update was not found.");
            if (existingSuite.IsSystem)
            {
                existingSuite = new NormalizationSuiteDefinition
                {
                    Id = Guid.NewGuid(),
                    Name = UniqueName(string.IsNullOrWhiteSpace(request.Proposal.SuggestedSuiteName)
                        ? $"{existingSuite.Name} extended"
                        : request.Proposal.SuggestedSuiteName),
                    Description = request.Proposal.SuggestedSuiteDescription ?? existingSuite.Description,
                    OperationIds = [.. existingSuite.OperationIds],
                    SequenceIds = [.. existingSuite.SequenceIds],
                    IsSystem = false,
                    IsDefault = false
                };
                clonedSystemSuite = true;
            }
            alreadyInSuite = SuiteOperationIds(existingSuite, sequences);
        }

        var operationIds = new List<Guid>();
        foreach (var proposed in request.Proposal.Operations)
        {
            if (proposed.ReuseOperationId is { } reuseId)
            {
                var existing = await normalizationStore.GetOperationByIdAsync(reuseId, cancellationToken);
                if (existing != null)
                {
                    if (!alreadyInSuite.Contains(existing.Id))
                        operationIds.Add(existing.Id);
                    continue;
                }
            }

            var extensionUrls = proposed.ExtensionUrls
                .Where(UploadedBundleAnalyzer.IsAbsoluteExtensionUrl)
                .ToList();
            if (string.Equals(proposed.OperationType, "RemoveExtensions", StringComparison.OrdinalIgnoreCase)
                && extensionUrls.Count == 0)
            {
                continue;
            }

            var op = new NormalizationOperationDefinition
            {
                Id = Guid.NewGuid(),
                Name = UniqueName(PrefixedName(request.Proposal.SuggestedSuiteName, proposed.SuggestedName)),
                Description = proposed.SuggestedDescription,
                OperationType = proposed.OperationType,
                ResourceTypes = proposed.ResourceTypes,
                SourceFhirPath = proposed.SourceFhirPath,
                TargetFhirPath = proposed.TargetFhirPath,
                ConditionTargetFhirPath = proposed.ConditionTargetFhirPath,
                ConditionTargetValue = CoerceJsonValue(proposed.ConditionTargetValue),
                Conditions = proposed.Conditions.Select(c => new NormalizationCondition
                {
                    FhirPathSource = c.FhirPathSource,
                    Operator = c.Operator,
                    Value = CoerceJsonValue(c.Value)
                }).ToList(),
                CodeMapFhirPath = proposed.CodeMapFhirPath,
                CodeSystemMaps = proposed.CodeSystemMaps,
                ExtensionUrls = extensionUrls,
                MaxIterations = proposed.MaxIterations,
                SplitOnComma = proposed.SplitOnComma,
                IsSystem = false,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            await normalizationStore.UpsertOperationAsync(op, cancellationToken);
            operationIds.Add(op.Id);
        }

        if (operationIds.Count == 0 && existingSuite != null)
        {
            if (clonedSystemSuite)
            {
                existingSuite.UpdatedAt = DateTimeOffset.UtcNow;
                await normalizationStore.UpsertSuiteAsync(existingSuite, cancellationToken);
            }
            return existingSuite;
        }

        if (operationIds.Count == 0)
            throw new InvalidOperationException("The normalization proposal has no operations to save.");

        var sequence = new NormalizationSequenceDefinition
        {
            Id = Guid.NewGuid(),
            Name = UniqueName(PrefixedName(request.Proposal.SuggestedSuiteName, request.Proposal.SuggestedSequenceName)),
            Description = request.Proposal.SuggestedSuiteDescription,
            Entries = operationIds
                .Select((id, idx) => new NormalizationSequenceEntry { OperationId = id, Sequence = idx + 1 })
                .ToList(),
            IsSystem = false,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await normalizationStore.UpsertSequenceAsync(sequence, cancellationToken);

        NormalizationSuiteDefinition suite;
        if (existingSuite != null)
        {
            suite = existingSuite;
            if (!suite.SequenceIds.Contains(sequence.Id))
                suite.SequenceIds.Add(sequence.Id);
            if (!string.IsNullOrWhiteSpace(request.Proposal.SuggestedSuiteDescription))
                suite.Description = request.Proposal.SuggestedSuiteDescription;
            if (!string.IsNullOrWhiteSpace(request.Proposal.SuggestedSuiteName))
                suite.Name = request.Proposal.SuggestedSuiteName.Trim();
        }
        else
        {
            suite = new NormalizationSuiteDefinition
            {
                Id = Guid.NewGuid(),
                Name = UniqueName(request.Proposal.SuggestedSuiteName),
                Description = request.Proposal.SuggestedSuiteDescription,
                SequenceIds = [sequence.Id],
                IsSystem = false,
                IsDefault = false
            };
        }

        suite.UpdatedAt = DateTimeOffset.UtcNow;
        await normalizationStore.UpsertSuiteAsync(suite, cancellationToken);
        return suite;
    }

    private static List<AnalyzeBundleSource> CollectSources(AnalyzeBundleConfigurationRequest request)
    {
        if (request.Sources is { Count: > 0 })
            return request.Sources.Where(HasContent).ToList();

        var single = new AnalyzeBundleSource
        {
            Source = request.Source,
            PatientId = request.PatientId,
            BundleJson = request.BundleJson,
            UploadedBundleId = request.UploadedBundleId
        };
        return HasContent(single) ? [single] : [];
    }

    private static bool HasContent(AnalyzeBundleSource source)
        => !string.IsNullOrWhiteSpace(source.BundleJson)
           || source.UploadedBundleId is { } id && id != Guid.Empty
           || !string.IsNullOrWhiteSpace(source.PatientId);

    private async Task<List<Resource>> LoadResourcesAsync(
        AnalyzeBundleSource source,
        CancellationToken cancellationToken)
    {
        var bundleJson = await LoadBundleJsonAsync(source, cancellationToken);
        if (string.IsNullOrWhiteSpace(bundleJson))
            throw new InvalidOperationException("No FHIR bundle content was provided.");

        Bundle? bundle;
        try
        {
            bundle = JsonSerializer.Deserialize<Bundle>(
                bundleJson,
                LantanaGroup.Link.Shared.Application.SerDes.LinkFhirSerializerOptions.ForFhirWithoutValidation());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse FHIR bundle: {ex.Message}", ex);
        }

        return bundle?.Entry?
            .Select(e => e.Resource)
            .Where(r => r != null)
            .Cast<Resource>()
            .ToList() ?? [];
    }

    private async Task<string> LoadBundleJsonAsync(
        AnalyzeBundleSource source,
        CancellationToken cancellationToken)
    {
        var kind = (source.Source ?? "").Trim();
        if (string.Equals(kind, "ExistingId", StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(source.PatientId) && string.IsNullOrWhiteSpace(source.BundleJson) && source.UploadedBundleId == null))
        {
            if (string.IsNullOrWhiteSpace(source.PatientId))
                throw new InvalidOperationException("patientId is required to analyze an existing FHIR ID.");
            var cfg = automationConfig.Value;
            var loader = new FhirDataLoader(cfg.FhirServerBase, cfg.FhirServerOAuth, cfg.FhirServerBasicAuth);
            return await loader.FetchPatientEverythingAsync(source.PatientId.Trim(), cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(source.BundleJson))
            return source.BundleJson!;

        if (source.UploadedBundleId is { } bundleId && bundleId != Guid.Empty)
        {
            var existing = await _bundles.Find(b => b.Id == bundleId).FirstOrDefaultAsync(cancellationToken);
            if (existing == null)
                throw new InvalidOperationException("Uploaded bundle was not found. Please re-upload.");
            return await bundleContentStore.ReadAsync(existing, cancellationToken)
                   ?? throw new InvalidOperationException("Uploaded bundle content is missing.");
        }

        throw new InvalidOperationException("Provide bundle JSON, an uploaded bundle id, or an existing FHIR patient id.");
    }

    private async System.Threading.Tasks.Task AddPostNormalizationPredictionNotesAsync(
        IReadOnlyList<Resource> resources,
        IReadOnlyList<NormalizationOperationDefinition> allOps,
        IReadOnlyList<NormalizationSequenceDefinition> sequences,
        NormalizationSuiteDefinition? refineSuite,
        GeneratedNormalizationProposal proposal,
        CancellationToken cancellationToken)
    {
        var domainResources = resources.OfType<DomainResource>().ToList();
        if (domainResources.Count == 0)
            return;

        var existingOps = ResolveSuiteOperationsInOrder(refineSuite, allOps, sequences);
        var workItems = NormalizationOperationMapper.ToWorkItems(existingOps, proposal.Operations);
        if (workItems.Count == 0)
            return;

        var clones = domainResources
            .Select(r => (DomainResource)r.DeepCopy())
            .ToList();

        try
        {
            await normalizationEngine.ApplyAllAsync(clones, workItems, cancellationToken);
        }
        catch (Exception ex)
        {
            proposal.Notes.Add($"Post-normalization prediction could not apply the suite ({ex.GetType().Name}: {ex.Message}). Eligibility notes above are based on raw upload data.");
            return;
        }

        var measures = new[] { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation };
        var before = ImportedPatientClassifier.Classify(ToEntries(domainResources), measures);
        var after = ImportedPatientClassifier.Classify(ToEntries(clones), measures);

        foreach (var measure in measures)
        {
            before.MeasureEligibilities.TryGetValue(measure, out var beforeElig);
            after.MeasureEligibilities.TryGetValue(measure, out var afterElig);
            if (beforeElig == afterElig)
                continue;

            proposal.Notes.Add(
                $"After applying this suite in-process, ACH eligibility changes {beforeElig} → {afterElig}. Predictions should follow the post-normalization result.");
        }
    }

    private static List<Bundle.EntryComponent> ToEntries(IEnumerable<Resource> resources)
        => resources.Select(r => new Bundle.EntryComponent { Resource = r }).ToList();

    private static List<NormalizationOperationDefinition> ResolveSuiteOperationsInOrder(
        NormalizationSuiteDefinition? suite,
        IReadOnlyList<NormalizationOperationDefinition> ops,
        IReadOnlyList<NormalizationSequenceDefinition> sequences)
    {
        if (suite == null)
            return [];

        var opsById = ops.ToDictionary(o => o.Id);
        var seqById = sequences.ToDictionary(s => s.Id);
        var ordered = new List<NormalizationOperationDefinition>();
        var seen = new HashSet<Guid>();

        foreach (var seqId in suite.SequenceIds)
        {
            if (!seqById.TryGetValue(seqId, out var seq))
                continue;
            foreach (var entry in seq.Entries.OrderBy(e => e.Sequence))
            {
                if (!seen.Add(entry.OperationId))
                    continue;
                if (opsById.TryGetValue(entry.OperationId, out var op))
                    ordered.Add(op);
            }
        }

        foreach (var id in suite.OperationIds)
        {
            if (!seen.Add(id))
                continue;
            if (opsById.TryGetValue(id, out var op))
                ordered.Add(op);
        }

        return ordered;
    }

    private static HashSet<Guid> SuiteOperationIds(
        NormalizationSuiteDefinition suite,
        IReadOnlyList<NormalizationSequenceDefinition> sequences)
    {
        var ids = suite.OperationIds.ToHashSet();
        var seqById = sequences.ToDictionary(s => s.Id);
        foreach (var seqId in suite.SequenceIds)
        {
            if (!seqById.TryGetValue(seqId, out var seq))
                continue;
            foreach (var entry in seq.Entries)
                ids.Add(entry.OperationId);
        }

        return ids;
    }

    private static int DistinctSystems(BundleConfigFingerprint fingerprint)
        => fingerprint.LocationIdentifiers
            .Select(i => i.System)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

    private static object? CoerceJsonValue(object? value)
    {
        if (value is not JsonElement element)
            return value;

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number when element.TryGetDouble(out var d) => d,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    private static string PrefixedName(string? prefix, string? name)
    {
        var trimmedPrefix = prefix?.Trim() ?? "";
        var trimmedName = name?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(trimmedPrefix) || LooksGeneric(trimmedPrefix))
            return trimmedName;
        if (string.IsNullOrWhiteSpace(trimmedName))
            return trimmedPrefix;
        if (trimmedName.StartsWith(trimmedPrefix, StringComparison.OrdinalIgnoreCase))
            return trimmedName;
        return $"{trimmedPrefix} - {trimmedName}";
    }

    private static string UniqueName(string? name)
    {
        var baseName = string.IsNullOrWhiteSpace(name) ? "Generated" : name.Trim();
        if (LooksGeneric(baseName))
            return $"{baseName} ({DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm})";
        return baseName;
    }

    private static bool LooksGeneric(string name)
        => name.StartsWith("Generated", StringComparison.OrdinalIgnoreCase)
           || string.Equals(name, "Generated patient sequence", StringComparison.OrdinalIgnoreCase);
}
