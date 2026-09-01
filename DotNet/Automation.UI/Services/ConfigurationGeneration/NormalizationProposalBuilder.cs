using Automation.UI.Models;

namespace Automation.UI.Services.ConfigurationGeneration;

public static class NormalizationProposalBuilder
{
    public static GeneratedNormalizationProposal Build(
        BundleConfigFingerprint fingerprint,
        IReadOnlyList<NormalizationOperationDefinition> existingOps,
        IReadOnlyList<NormalizationSuiteDefinition> existingSuites,
        IReadOnlyList<NormalizationSequenceDefinition>? sequences = null,
        NormalizationSuiteDefinition? refineExisting = null)
    {
        var proposal = new GeneratedNormalizationProposal
        {
            SuggestedSuiteName = refineExisting is { IsSystem: false }
                ? refineExisting.Name
                : "",
            SuggestedSuiteDescription = "Auto-built from structures found in the uploaded bundle. Review before use.",
            SuggestedSequenceName = "Generated patient sequence"
        };

        var existingSuiteTypes = SuiteOperationTypes(refineExisting, existingOps, sequences);
        var existingSuiteExtensionUrls = SuiteExtensionUrls(refineExisting, existingOps, sequences);

        TryAddCopyLocation(proposal, fingerprint, existingOps, existingSuiteTypes);
        TryAddCopyAlias(proposal, fingerprint, existingOps, existingSuiteTypes);
        TryAddCodeMap(proposal, fingerprint, existingOps, existingSuiteTypes);
        TryAddRemoveExtensions(proposal, fingerprint, existingOps, existingSuiteTypes, existingSuiteExtensionUrls);
        AddEligibilityGuardNotes(proposal, fingerprint, existingOps, sequences, refineExisting);

        if (proposal.Operations.Count == 0)
        {
            proposal.Notes.Add(refineExisting != null
                ? "The selected suite already covers every normalization opportunity found in this upload."
                : "No normalization opportunities were found in the uploaded data.");
        }
        else
        {
            proposal.Notes.Add($"Proposed {proposal.Operations.Count} operation(s), preferring additive mapping helpers the data can support. Eligibility-critical Encounter/Location fields are left unchanged.");
            if (refineExisting != null)
                proposal.Notes.Add($"Only operations not already covered by '{refineExisting.Name}' are listed so the suite can be extended.");
        }

        proposal.Reuse = ScoreSuites(proposal, existingSuites, existingOps, sequences);
        return proposal;
    }

    private static void TryAddCopyLocation(
        GeneratedNormalizationProposal proposal,
        BundleConfigFingerprint fingerprint,
        IReadOnlyList<NormalizationOperationDefinition> existingOps,
        HashSet<string> existingSuiteTypes)
    {
        if (fingerprint.LocationIdentifiers.Count == 0)
            return;
        if (existingSuiteTypes.Contains("CopyLocation"))
            return;

        var reuse = existingOps.FirstOrDefault(o =>
            string.Equals(o.OperationType, "CopyLocation", StringComparison.OrdinalIgnoreCase)
            && o.ResourceTypes.Contains("Location", StringComparer.OrdinalIgnoreCase));

        proposal.Operations.Add(new GeneratedNormalizationOperationProposal
        {
            OperationType = "CopyLocation",
            SuggestedName = "Copy Location identifiers to type",
            SuggestedDescription = "Locations in the upload carry identifiers. CopyLocation adds those as extra type CodeableConcepts without replacing existing type codes.",
            ResourceTypes = ["Location"],
            ReuseOperationId = reuse?.Id,
            ReuseOperationName = reuse?.Name
        });
    }

    private static void TryAddCopyAlias(
        GeneratedNormalizationProposal proposal,
        BundleConfigFingerprint fingerprint,
        IReadOnlyList<NormalizationOperationDefinition> existingOps,
        HashSet<string> existingSuiteTypes)
    {
        if (fingerprint.LocationAliases.Count == 0)
            return;
        if (existingSuiteTypes.Contains("CopyLocationAliasToTypeIteratively"))
            return;

        var reuse = existingOps.FirstOrDefault(o =>
            string.Equals(o.OperationType, "CopyLocationAliasToTypeIteratively", StringComparison.OrdinalIgnoreCase));

        proposal.Operations.Add(new GeneratedNormalizationOperationProposal
        {
            OperationType = "CopyLocationAliasToTypeIteratively",
            SuggestedName = "Copy Location aliases to type iteratively",
            SuggestedDescription = $"Locations include alias values ({string.Join(", ", fingerprint.LocationAliases.Take(3))}). Iterative copy adds those as extra type codes without replacing existing ones.",
            ResourceTypes = ["Location"],
            MaxIterations = 15,
            SplitOnComma = fingerprint.LocationAliases.Any(a => a.Contains(',')),
            ReuseOperationId = reuse?.Id,
            ReuseOperationName = reuse?.Name
        });
    }

    private static void TryAddCodeMap(
        GeneratedNormalizationProposal proposal,
        BundleConfigFingerprint fingerprint,
        IReadOnlyList<NormalizationOperationDefinition> existingOps,
        HashSet<string> existingSuiteTypes)
    {
        if (existingSuiteTypes.Contains("CodeMap"))
            return;

        // CopyLocation (already in the suite, or proposed earlier in this same pass) writes
        // identifier system/value onto Location.type. CodeMap runs after that, so identity-mapping
        // original upload type systems (often v3-RoleCode on a parent location) finds nothing.
        var copyLocationWillRun = existingSuiteTypes.Contains("CopyLocation")
            || proposal.Operations.Any(o =>
                string.Equals(o.OperationType, "CopyLocation", StringComparison.OrdinalIgnoreCase));

        string sourceSystem;
        Dictionary<string, NormalizationCodeMapEntry> maps;
        string description;
        if (copyLocationWillRun && fingerprint.LocationIdentifiers.Count > 0)
        {
            var bySystem = fingerprint.LocationIdentifiers
                .Where(i => !string.IsNullOrWhiteSpace(i.System) && !string.IsNullOrWhiteSpace(i.Value))
                .GroupBy(i => i.System, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Select(i => i.Value).Distinct(StringComparer.Ordinal).Count())
                .FirstOrDefault();
            if (bySystem == null)
                return;

            sourceSystem = bySystem.Key;
            maps = new Dictionary<string, NormalizationCodeMapEntry>(StringComparer.Ordinal);
            foreach (var identifier in bySystem)
            {
                maps[identifier.Value] = new NormalizationCodeMapEntry
                {
                    Code = identifier.Value,
                    Display = identifier.Value
                };
            }

            description = "Identity map seeded from Location identifiers. CopyLocation writes those onto type.coding before this CodeMap runs.";
        }
        else
        {
            var locationCodes = fingerprint.Codings
                .Where(c => string.Equals(c.ResourceType, "Location", StringComparison.OrdinalIgnoreCase)
                            && string.Equals(c.Path, "type.coding", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (locationCodes.Count == 0)
                return;

            var bySystem = locationCodes
                .GroupBy(c => c.System, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .First();

            sourceSystem = bySystem.Key;
            maps = new Dictionary<string, NormalizationCodeMapEntry>(StringComparer.Ordinal);
            foreach (var coding in bySystem)
            {
                maps[coding.Code] = new NormalizationCodeMapEntry
                {
                    Code = coding.Code,
                    Display = coding.Display ?? coding.Code
                };
            }

            description = "Identity map seeded from Location.type codes in the upload. Review and replace targets if a different code system is required.";
        }

        if (maps.Count == 0 || string.IsNullOrWhiteSpace(sourceSystem))
            return;

        var reuse = existingOps.FirstOrDefault(o =>
            string.Equals(o.OperationType, "CodeMap", StringComparison.OrdinalIgnoreCase)
            && string.Equals(o.CodeMapFhirPath, "type.coding", StringComparison.OrdinalIgnoreCase)
            && o.CodeSystemMaps.Any(m =>
                string.Equals(m.SourceSystem, sourceSystem, StringComparison.OrdinalIgnoreCase)));

        proposal.Operations.Add(new GeneratedNormalizationOperationProposal
        {
            OperationType = "CodeMap",
            SuggestedName = $"Code map Location.type ({sourceSystem})",
            SuggestedDescription = description,
            ResourceTypes = ["Location"],
            CodeMapFhirPath = "type.coding",
            CodeSystemMaps =
            [
                new NormalizationCodeSystemMap
                {
                    SourceSystem = sourceSystem,
                    TargetSystem = sourceSystem,
                    CodeMaps = maps
                }
            ],
            ReuseOperationId = reuse?.Id,
            ReuseOperationName = reuse?.Name
        });
    }

    private static void TryAddRemoveExtensions(
        GeneratedNormalizationProposal proposal,
        BundleConfigFingerprint fingerprint,
        IReadOnlyList<NormalizationOperationDefinition> existingOps,
        HashSet<string> existingSuiteTypes,
        HashSet<string> existingSuiteExtensionUrls)
    {
        if (fingerprint.Extensions.Count == 0)
            return;

        var observed = fingerprint.Extensions
            .Select(e => e.Url?.Trim() ?? "")
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var invalid = observed.Where(url => !UploadedBundleAnalyzer.IsAbsoluteExtensionUrl(url)).ToList();
        var urls = observed
            .Where(UploadedBundleAnalyzer.IsAbsoluteExtensionUrl)
            .Where(url => !existingSuiteExtensionUrls.Contains(url))
            .OrderBy(u => u, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (invalid.Count > 0)
            proposal.Notes.Add($"Skipped {invalid.Count} relative/invalid extension URL(s); RemoveExtensions only accepts absolute URLs.");
        if (urls.Count == 0)
            return;

        var types = fingerprint.Extensions
            .Where(e => urls.Contains(e.Url, StringComparer.OrdinalIgnoreCase))
            .Select(e => e.ResourceType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var reuse = existingOps.FirstOrDefault(o =>
            string.Equals(o.OperationType, "RemoveExtensions", StringComparison.OrdinalIgnoreCase)
            && urls.All(url => o.ExtensionUrls.Contains(url, StringComparer.OrdinalIgnoreCase)));

        proposal.Operations.Add(new GeneratedNormalizationOperationProposal
        {
            OperationType = "RemoveExtensions",
            SuggestedName = "Remove extensions found in upload",
            SuggestedDescription = $"Removes {urls.Count} extension URL(s) observed on {string.Join(", ", types)}.",
            ResourceTypes = types,
            ExtensionUrls = urls,
            ReuseOperationId = reuse?.Id,
            ReuseOperationName = reuse?.Name
        });
    }

    private static List<ReuseCandidate> ScoreSuites(
        GeneratedNormalizationProposal proposal,
        IReadOnlyList<NormalizationSuiteDefinition> suites,
        IReadOnlyList<NormalizationOperationDefinition> ops,
        IReadOnlyList<NormalizationSequenceDefinition>? sequences)
    {
        var neededTypes = proposal.Operations
            .Select(o => o.OperationType)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (neededTypes.Count == 0)
            return [];

        var opsById = ops.ToDictionary(o => o.Id);
        var seqById = (sequences ?? []).ToDictionary(s => s.Id);
        var results = new List<ReuseCandidate>();
        foreach (var suite in suites)
        {
            var suiteOpIds = suite.OperationIds.ToList();
            foreach (var seqId in suite.SequenceIds)
            {
                if (!seqById.TryGetValue(seqId, out var seq))
                    continue;
                suiteOpIds.AddRange(seq.Entries.Select(e => e.OperationId));
            }

            var suiteOps = suiteOpIds
                .Distinct()
                .Select(id => opsById.GetValueOrDefault(id))
                .Where(o => o != null)
                .Cast<NormalizationOperationDefinition>()
                .ToList();
            var types = suiteOps.Select(o => o.OperationType).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var hit = neededTypes.Count(types.Contains);
            if (hit == 0)
                continue;

            var score = (double)hit / neededTypes.Count;
            results.Add(new ReuseCandidate
            {
                Id = suite.Id,
                Name = suite.Name,
                Kind = suite.IsSystem ? "System suite" : "Custom suite",
                Score = Math.Round(score, 2),
                Recommendation = score >= 0.999 ? "Reuse" : "Extend",
                Reason = score >= 0.999
                    ? "This suite already includes every proposed operation type."
                    : suite.IsSystem
                        ? $"This system suite includes {hit} of {neededTypes.Count} proposed operation types. Extending clones a custom copy so the system suite stays unchanged."
                        : $"This suite includes {hit} of {neededTypes.Count} proposed operation types."
            });
        }

        return results
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
    }

    /// <summary>
    /// Auto-generated suites must not rewrite Encounter class/status/period or overwrite
    /// Location.type codes. Those fields are what ACH IP CQL uses; a FHIRPath write that
    /// lands incorrectly can drop a qualifying patient from the report.
    /// </summary>
    private static void AddEligibilityGuardNotes(
        GeneratedNormalizationProposal proposal,
        BundleConfigFingerprint fingerprint,
        IReadOnlyList<NormalizationOperationDefinition> existingOps,
        IReadOnlyList<NormalizationSequenceDefinition>? sequences,
        NormalizationSuiteDefinition? refineExisting)
    {
        var hasEncounterClass = fingerprint.Codings.Any(c =>
            string.Equals(c.ResourceType, "Encounter", StringComparison.OrdinalIgnoreCase)
            && string.Equals(c.Path, "class", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(c.Code));
        if (hasEncounterClass)
        {
            proposal.Notes.Add(
                "Encounter.class is present. Generated suites will not rewrite Encounter.status or class — those values already drive measure eligibility.");
        }

        if (fingerprint.LocationIdentifiers.Count > 0)
        {
            proposal.Notes.Add(
                "Location identifiers will not be copied over existing Location.type codes. Overwriting type[0].coding.code can remove HSLOC (or other) codes the measure uses for initial population.");
        }

        var rewritesLocationType = proposal.Operations.Any(o =>
            string.Equals(o.OperationType, "CopyLocation", StringComparison.OrdinalIgnoreCase)
            || string.Equals(o.OperationType, "CopyLocationAliasToTypeIteratively", StringComparison.OrdinalIgnoreCase)
            || (string.Equals(o.OperationType, "CodeMap", StringComparison.OrdinalIgnoreCase)
                && o.ResourceTypes.Contains("Location", StringComparer.OrdinalIgnoreCase)));
        if (rewritesLocationType)
        {
            proposal.Notes.Add(
                "Copy Location, alias copy, and Location CodeMap change type after acquisition. Org resource maps must match identifiers or type codes already on the raw Location; they will not see codes these operations add.");
        }

        if (refineExisting == null)
            return;

        var suiteOps = ResolveSuiteOperations(refineExisting, existingOps, sequences);
        var risky = suiteOps
            .Where(IsEligibilityCriticalWrite)
            .Select(o => $"{o.OperationType} '{o.Name}'")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (risky.Count == 0)
            return;

        proposal.Notes.Add(
            $"The suite being extended already contains eligibility-critical write(s): {string.Join("; ", risky)}. Those can make a qualifying patient ineligible after normalization. Review them before reuse.");
    }

    private static bool IsEligibilityCriticalWrite(NormalizationOperationDefinition op)
    {
        if (string.Equals(op.OperationType, "ConditionalTransform", StringComparison.OrdinalIgnoreCase)
            && op.ResourceTypes.Contains("Encounter", StringComparer.OrdinalIgnoreCase))
        {
            var target = op.ConditionTargetFhirPath ?? op.TargetFhirPath ?? "";
            return target.Contains("status", StringComparison.OrdinalIgnoreCase)
                   || target.Contains("class", StringComparison.OrdinalIgnoreCase)
                   || target.Contains("period", StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(op.OperationType, "CopyProperty", StringComparison.OrdinalIgnoreCase)
            && op.ResourceTypes.Contains("Location", StringComparer.OrdinalIgnoreCase))
        {
            var target = op.TargetFhirPath ?? "";
            return target.Contains("type[", StringComparison.OrdinalIgnoreCase)
                   && target.Contains("coding.code", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static HashSet<string> SuiteOperationTypes(
        NormalizationSuiteDefinition? suite,
        IReadOnlyList<NormalizationOperationDefinition> ops,
        IReadOnlyList<NormalizationSequenceDefinition>? sequences)
        => ResolveSuiteOperations(suite, ops, sequences)
            .Select(o => o.OperationType)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static HashSet<string> SuiteExtensionUrls(
        NormalizationSuiteDefinition? suite,
        IReadOnlyList<NormalizationOperationDefinition> ops,
        IReadOnlyList<NormalizationSequenceDefinition>? sequences)
        => ResolveSuiteOperations(suite, ops, sequences)
            .Where(o => string.Equals(o.OperationType, "RemoveExtensions", StringComparison.OrdinalIgnoreCase))
            .SelectMany(o => o.ExtensionUrls)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static List<NormalizationOperationDefinition> ResolveSuiteOperations(
        NormalizationSuiteDefinition? suite,
        IReadOnlyList<NormalizationOperationDefinition> ops,
        IReadOnlyList<NormalizationSequenceDefinition>? sequences)
    {
        if (suite == null)
            return [];

        var opsById = ops.ToDictionary(o => o.Id);
        var seqById = (sequences ?? []).ToDictionary(s => s.Id);
        var ids = suite.OperationIds.ToList();
        foreach (var seqId in suite.SequenceIds)
        {
            if (!seqById.TryGetValue(seqId, out var seq))
                continue;
            ids.AddRange(seq.Entries.Select(e => e.OperationId));
        }

        return ids
            .Distinct()
            .Select(id => opsById.GetValueOrDefault(id))
            .Where(o => o != null)
            .Cast<NormalizationOperationDefinition>()
            .ToList();
    }
}
