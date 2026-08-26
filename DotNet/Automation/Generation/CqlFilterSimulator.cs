namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Simulates measure CQL instance filtering at generation time by evaluating
/// inclusion rules derived from the measure bundle CQL that MeasureEval will run.
///
/// Type-level reachability (<c>[ResourceType]</c>) is handled elsewhere. This
/// simulator focuses on per-resource exclusions from SDE <c>where</c> predicates.
///
/// Operates on extracted resource attributes (<see cref="CqlFilterInputExtractor"/>).
/// </summary>
public static class CqlFilterSimulator
{
    /// <summary>
    /// Computes resource keys the selected measures' CQL will exclude, using each
    /// measure family's embedded bundle. Prefer
    /// <see cref="ComputeFilteredKeys(IReadOnlyList{string}, PatientCqlInput)"/>
    /// when the run has uploaded/edited measure JSON.
    /// </summary>
    public static HashSet<string> ComputeFilteredKeys(
        IReadOnlyList<ProfiledMeasureType> measures,
        PatientCqlInput input)
    {
        if (measures == null || measures.Count == 0 || input == null)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var bundles = measures
            .Select(measure => ProfiledMeasureCatalog.ReadBundleJson(measure))
            .ToList();
        return ComputeFilteredKeys(bundles, input);
    }

    /// <summary>
    /// Computes resource keys CQL SDE <c>where</c> clauses will exclude, using the
    /// actual measure bundle JSON evaluated for this run.
    ///
    /// Each bundle is analyzed independently (its own Initial Population and SDE
    /// defines). A key is excluded only when every bundle that produces inclusion
    /// rules for that resource type excludes it — matching MeasureEval writing one
    /// <c>.mr</c> file per measure and PatientAggregator unioning contained resources.
    /// </summary>
    public static HashSet<string> ComputeFilteredKeys(
        IReadOnlyList<string> measureBundleJsons,
        PatientCqlInput input)
    {
        if (measureBundleJsons == null || measureBundleJsons.Count == 0 || input == null)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var perTypeExclusions = new Dictionary<string, List<HashSet<string>>>(StringComparer.OrdinalIgnoreCase);

        foreach (var json in measureBundleJsons)
        {
            if (string.IsNullOrWhiteSpace(json))
                continue;

            CqlInstanceFilterAnalyzer.MeasureFilterModel model;
            try
            {
                model = CqlInstanceFilterAnalyzer.Analyze(json);
            }
            catch
            {
                continue;
            }

            if (model.Rules.Count == 0)
                continue;

            var ipWindows = ResolveIpWindows(model, input);
            var enriched = input with { IpWindows = ipWindows };
            var included = EvaluateIncludedKeys(model, enriched);

            foreach (var group in model.Rules.GroupBy(r => r.ResourceType, StringComparer.OrdinalIgnoreCase))
            {
                var resourceType = group.Key;
                var candidates = CandidateKeys(enriched, resourceType);
                if (candidates.Count == 0)
                    continue;

                if (!perTypeExclusions.TryGetValue(resourceType, out var bucket))
                {
                    bucket = [];
                    perTypeExclusions[resourceType] = bucket;
                }

                var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var key in candidates)
                {
                    if (!included.Contains(key))
                        excluded.Add(key);
                }

                bucket.Add(excluded);
            }
        }

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var bucket in perTypeExclusions.Values)
        {
            var intersection = new HashSet<string>(bucket[0], StringComparer.OrdinalIgnoreCase);
            for (var i = 1; i < bucket.Count; i++)
                intersection.IntersectWith(bucket[i]);
            foreach (var key in intersection)
                result.Add(key);
        }

        return result;
    }

    private static IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> ResolveIpWindows(
        CqlInstanceFilterAnalyzer.MeasureFilterModel model,
        PatientCqlInput input)
    {
        if (input.Encounters == null || input.Encounters.Count == 0)
        {
            if (!string.IsNullOrEmpty(input.EncounterId))
                return [new MeasureInitialPopulationResolver.IpWindow(input.EncounterId, input.EncounterStart, input.EncounterEnd)];
            return [];
        }

        var windows = new List<MeasureInitialPopulationResolver.IpWindow>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var enc in input.Encounters)
        {
            if (enc == null || string.IsNullOrEmpty(enc.EncounterId))
                continue;
            if (!IsIpEncounter(enc, model, input))
                continue;
            if (!seen.Add(enc.EncounterId))
                continue;
            windows.Add(new MeasureInitialPopulationResolver.IpWindow(enc.EncounterId, enc.PeriodStart, enc.PeriodEnd));
        }

        return windows;
    }

    private static bool IsIpEncounter(
        EncounterContext enc,
        CqlInstanceFilterAnalyzer.MeasureFilterModel model,
        PatientCqlInput input)
    {
        if (model.IpStatusCodes.Count > 0
            && !model.IpStatusCodes.Contains(enc.Status)
            && !StatusMatchesFhirEnum(enc.Status, model.IpStatusCodes))
        {
            return false;
        }

        var mpStart = input.MeasurementPeriodStart;
        var mpEnd = input.MeasurementPeriodEnd;
        if (!(mpStart == DateTime.MinValue && mpEnd == DateTime.MaxValue)
            && !(enc.PeriodStart <= mpEnd && enc.PeriodEnd >= mpStart))
        {
            return false;
        }

        if (model.IpAllowsAnyClass || model.IpClassCodes.Count == 0)
            return true;

        return model.IpClassCodes.Contains(enc.ClassCode);
    }

    private static bool StatusMatchesFhirEnum(string status, IReadOnlySet<string> allowed)
    {
        foreach (var code in allowed)
        {
            if (string.Equals(status, code.Replace("-", string.Empty), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static HashSet<string> EvaluateIncludedKeys(
        CqlInstanceFilterAnalyzer.MeasureFilterModel model,
        PatientCqlInput input)
    {
        var included = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in model.Rules.Where(r => !r.SpecimenFromMatchingObservations))
        {
            switch (rule.ResourceType)
            {
                case "Condition":
                    foreach (var c in input.Conditions)
                    {
                        if (MatchesCondition(c, rule, input))
                            included.Add($"Condition/{c.ResourceId}");
                    }
                    break;
                case "Observation":
                    foreach (var o in input.Observations)
                    {
                        if (MatchesObservation(o, rule, input))
                            included.Add($"Observation/{o.ResourceId}");
                    }
                    break;
                case "DiagnosticReport":
                    foreach (var d in input.DiagnosticReports)
                    {
                        if (MatchesDiagnosticReport(d, rule, input))
                            included.Add($"DiagnosticReport/{d.ResourceId}");
                    }
                    break;
                case "Procedure":
                    foreach (var p in input.Procedures)
                    {
                        if (MatchesProcedure(p, rule, input))
                            included.Add($"Procedure/{p.ResourceId}");
                    }
                    break;
                case "MedicationRequest":
                    foreach (var m in input.MedicationRequests)
                    {
                        if (MatchesMedicationRequest(m, rule, input))
                            included.Add($"MedicationRequest/{m.ResourceId}");
                    }
                    break;
                case "MedicationAdministration":
                    foreach (var m in input.MedicationAdministrations)
                    {
                        if (MatchesMedicationAdministration(m, rule, input))
                            included.Add($"MedicationAdministration/{m.ResourceId}");
                    }
                    break;
                case "Coverage":
                    foreach (var c in input.Coverages)
                    {
                        if (MatchesCoverage(c, rule, input))
                            included.Add($"Coverage/{c.ResourceId}");
                    }
                    break;
                case "ServiceRequest":
                    foreach (var s in input.ServiceRequests)
                    {
                        if (MatchesServiceRequest(s, rule, input))
                            included.Add($"ServiceRequest/{s.ResourceId}");
                    }
                    break;
                case "Encounter":
                    foreach (var enc in EffectiveEncounters(input))
                    {
                        if (MatchesEncounter(enc, rule, input))
                            included.Add($"Encounter/{enc.EncounterId}");
                    }
                    break;
                case "Specimen":
                    foreach (var s in input.Specimens)
                    {
                        if (MatchesSpecimen(s, rule, input))
                            included.Add($"Specimen/{s.ResourceId}");
                    }
                    break;
            }
        }

        foreach (var rule in model.Rules.Where(r => r.SpecimenFromMatchingObservations))
        {
            var sourceObs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var obsRule in rule.ObservationSourceRules)
            {
                foreach (var o in input.Observations)
                {
                    if (MatchesObservation(o, obsRule, input))
                        sourceObs.Add(o.ResourceId);
                }
            }

            if (sourceObs.Count == 0)
                continue;

            foreach (var o in input.Observations)
            {
                if (!sourceObs.Contains(o.ResourceId))
                    continue;
                var specimenId = ReferenceId(o.SpecimenReference);
                if (string.IsNullOrWhiteSpace(specimenId))
                    continue;
                var specimen = input.Specimens.FirstOrDefault(s =>
                    string.Equals(s.ResourceId, specimenId, StringComparison.OrdinalIgnoreCase));
                if (specimen == null)
                    continue;
                if (rule.SubjectMustBePatient && !ReferencesPatient(specimen.SubjectReference, input.PatientId))
                    continue;
                included.Add($"Specimen/{specimen.ResourceId}");
            }
        }

        return included;
    }

    private static HashSet<string> CandidateKeys(PatientCqlInput input, string resourceType) => resourceType switch
    {
        "Condition" => input.Conditions.Select(c => $"Condition/{c.ResourceId}").ToHashSet(StringComparer.OrdinalIgnoreCase),
        "Observation" => input.Observations.Select(o => $"Observation/{o.ResourceId}").ToHashSet(StringComparer.OrdinalIgnoreCase),
        "DiagnosticReport" => input.DiagnosticReports.Select(d => $"DiagnosticReport/{d.ResourceId}").ToHashSet(StringComparer.OrdinalIgnoreCase),
        "Procedure" => input.Procedures.Select(p => $"Procedure/{p.ResourceId}").ToHashSet(StringComparer.OrdinalIgnoreCase),
        "MedicationRequest" => input.MedicationRequests.Select(m => $"MedicationRequest/{m.ResourceId}").ToHashSet(StringComparer.OrdinalIgnoreCase),
        "MedicationAdministration" => input.MedicationAdministrations.Select(m => $"MedicationAdministration/{m.ResourceId}").ToHashSet(StringComparer.OrdinalIgnoreCase),
        "Coverage" => input.Coverages.Select(c => $"Coverage/{c.ResourceId}").ToHashSet(StringComparer.OrdinalIgnoreCase),
        "ServiceRequest" => input.ServiceRequests.Select(s => $"ServiceRequest/{s.ResourceId}").ToHashSet(StringComparer.OrdinalIgnoreCase),
        "Encounter" => EffectiveEncounters(input).Select(e => $"Encounter/{e.EncounterId}").ToHashSet(StringComparer.OrdinalIgnoreCase),
        "Specimen" => input.Specimens.Select(s => $"Specimen/{s.ResourceId}").ToHashSet(StringComparer.OrdinalIgnoreCase),
        _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    };

    private static IEnumerable<EncounterContext> EffectiveEncounters(PatientCqlInput input)
    {
        if (input.Encounters != null && input.Encounters.Count > 0)
            return input.Encounters;
        if (!string.IsNullOrEmpty(input.EncounterId))
            return [new EncounterContext(input.EncounterId, input.EncounterStart, input.EncounterEnd, string.Empty, "finished")];
        return [];
    }

    private static bool MatchesCondition(ConditionContext c, CqlInstanceFilterAnalyzer.CqlInclusionRule rule, PatientCqlInput input)
    {
        var start = c.OnsetStart == default ? c.RecordedDate : c.OnsetStart;
        var end = c.OnsetEnd == default ? start : c.OnsetEnd;
        if (!PassesCommon(rule, input, start, end, c.EncounterReference, c.CategoryCodes, status: c.IsActive ? "active" : string.Empty, codes: []))
            return false;
        if (rule.RequireEncounterLinkedToIp && !input.IpWindows.AnyEncounterMatches(c.EncounterReference))
            return false;
        return true;
    }

    private static bool MatchesObservation(ObservationContext o, CqlInstanceFilterAnalyzer.CqlInclusionRule rule, PatientCqlInput input)
        => PassesCommon(
            rule,
            input,
            o.EffectiveStart,
            o.EffectiveEnd,
            encounterReference: null,
            o.CategoryCodes,
            o.Status,
            codes: string.IsNullOrWhiteSpace(o.LoincCode) ? [] : [o.LoincCode]);

    private static bool MatchesDiagnosticReport(DiagnosticReportContext d, CqlInstanceFilterAnalyzer.CqlInclusionRule rule, PatientCqlInput input)
        => PassesCommon(rule, input, d.EffectiveStart, d.EffectiveEnd, null, d.CategoryCodes, status: null, codes: []);

    private static bool MatchesProcedure(ProcedureContext p, CqlInstanceFilterAnalyzer.CqlInclusionRule rule, PatientCqlInput input)
        => PassesCommon(rule, input, p.PerformedStart, p.PerformedEnd, p.EncounterReference, categories: [], status: null, p.Codes);

    private static bool MatchesMedicationRequest(MedicationRequestContext m, CqlInstanceFilterAnalyzer.CqlInclusionRule rule, PatientCqlInput input)
        => PassesCommon(rule, input, m.AuthoredOn, m.AuthoredOn, m.EncounterReference, categories: [], status: null, m.MedicationCodes, m.Intent);

    private static bool MatchesMedicationAdministration(MedicationAdministrationContext m, CqlInstanceFilterAnalyzer.CqlInclusionRule rule, PatientCqlInput input)
        => PassesCommon(rule, input, m.EffectiveStart, m.EffectiveEnd, m.EncounterReference, categories: [], m.Status, m.MedicationCodes);

    private static bool MatchesCoverage(CoverageContext c, CqlInstanceFilterAnalyzer.CqlInclusionRule rule, PatientCqlInput input)
        => PassesCommon(rule, input, c.PeriodStart, c.PeriodEnd, null, categories: [], status: null, codes: []);

    private static bool MatchesServiceRequest(ServiceRequestContext s, CqlInstanceFilterAnalyzer.CqlInclusionRule rule, PatientCqlInput input)
        => PassesCommon(rule, input, s.AuthoredOn, s.AuthoredOn, s.EncounterReference, categories: [], status: null, s.Codes, s.Intent);

    private static bool MatchesEncounter(EncounterContext enc, CqlInstanceFilterAnalyzer.CqlInclusionRule rule, PatientCqlInput input)
    {
        if (rule.RequireIpExists && input.IpWindows.Count == 0)
            return false;
        if (rule.MustBeIpEncounter)
            return input.IpWindows.AnyEncounterMatches(enc.EncounterId);
        if (rule.Date is CqlInstanceFilterAnalyzer.DateRelation.OverlapsIpPeriod
            or CqlInstanceFilterAnalyzer.DateRelation.DuringIpPeriod
            or CqlInstanceFilterAnalyzer.DateRelation.None)
        {
            return input.IpWindows.AnyOverlaps(enc.PeriodStart, enc.PeriodEnd);
        }

        return PassesDate(rule.Date, enc.PeriodStart, enc.PeriodEnd, input);
    }

    private static bool MatchesSpecimen(SpecimenContext s, CqlInstanceFilterAnalyzer.CqlInclusionRule rule, PatientCqlInput input)
    {
        if (rule.SubjectMustBePatient && !ReferencesPatient(s.SubjectReference, input.PatientId))
            return false;
        return PassesCommon(rule, input, s.CollectionStart, s.CollectionEnd, null, categories: [], status: null, codes: []);
    }

    private static bool PassesCommon(
        CqlInstanceFilterAnalyzer.CqlInclusionRule rule,
        PatientCqlInput input,
        DateTime start,
        DateTime end,
        string? encounterReference,
        IReadOnlyList<string> categories,
        string? status,
        IReadOnlyList<string> codes,
        string? intent = null)
    {
        if (rule.RequireIpExists && input.IpWindows.Count == 0)
            return false;
        if (rule.CategoryAnyOf is { Count: > 0 } && !categories.Any(c => rule.CategoryAnyOf.Contains(c)))
            return false;
        if (rule.StatusAnyOf is { Count: > 0 }
            && (string.IsNullOrWhiteSpace(status)
                || (!rule.StatusAnyOf.Contains(status) && !StatusMatchesFhirEnum(status, rule.StatusAnyOf))))
        {
            return false;
        }

        if (rule.IntentAnyOf is { Count: > 0 }
            && (string.IsNullOrWhiteSpace(intent) || !rule.IntentAnyOf.Contains(intent)))
        {
            return false;
        }

        if (rule.CodeAnyOf is { Count: > 0 } && !codes.Any(c => rule.CodeAnyOf.Contains(c)))
            return false;
        if (rule.RequireEncounterLinkedToIp
            && !input.IpWindows.AnyEncounterMatches(encounterReference))
        {
            return false;
        }

        return PassesDate(rule.Date, start, end, input);
    }

    private static bool PassesDate(
        CqlInstanceFilterAnalyzer.DateRelation date,
        DateTime start,
        DateTime end,
        PatientCqlInput input)
    {
        switch (date)
        {
            case CqlInstanceFilterAnalyzer.DateRelation.None:
                return true;
            case CqlInstanceFilterAnalyzer.DateRelation.OverlapsIpPeriod:
                return input.IpWindows.AnyOverlaps(start, end);
            case CqlInstanceFilterAnalyzer.DateRelation.DuringIpPeriod:
                return input.IpWindows.AnyContains(start, end);
            case CqlInstanceFilterAnalyzer.DateRelation.StartDuringIpPeriod:
                return input.IpWindows.AnyContains(start);
            case CqlInstanceFilterAnalyzer.DateRelation.DuringMeasurementPeriod:
                return start >= input.MeasurementPeriodStart && end <= input.MeasurementPeriodEnd;
            case CqlInstanceFilterAnalyzer.DateRelation.OverlapsMeasurementPeriod:
                return start <= input.MeasurementPeriodEnd && end >= input.MeasurementPeriodStart;
            case CqlInstanceFilterAnalyzer.DateRelation.CoverageActiveAtIpEnd:
                return input.IpWindows.Any(w => start <= w.End && end >= w.End);
            default:
                return true;
        }
    }

    private static string ReferenceId(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return string.Empty;
        var slash = reference.IndexOf('/');
        return slash >= 0 ? reference[(slash + 1)..] : reference;
    }

    private static bool ReferencesPatient(string? reference, string patientId) =>
        !string.IsNullOrWhiteSpace(patientId)
        && string.Equals(ReferenceId(reference), patientId, StringComparison.OrdinalIgnoreCase);

    public sealed record PatientCqlInput(
        string PatientId,
        string EncounterId,
        DateTime EncounterStart,
        DateTime EncounterEnd,
        IReadOnlyList<ConditionContext> Conditions,
        IReadOnlyList<ObservationContext> Observations)
    {
        public IReadOnlyList<DiagnosticReportContext> DiagnosticReports { get; init; } = Array.Empty<DiagnosticReportContext>();
        public IReadOnlyList<ProcedureContext> Procedures { get; init; } = Array.Empty<ProcedureContext>();
        public IReadOnlyList<MedicationRequestContext> MedicationRequests { get; init; } = Array.Empty<MedicationRequestContext>();
        public IReadOnlyList<MedicationAdministrationContext> MedicationAdministrations { get; init; } = Array.Empty<MedicationAdministrationContext>();
        public IReadOnlyList<CoverageContext> Coverages { get; init; } = Array.Empty<CoverageContext>();
        public IReadOnlyList<ServiceRequestContext> ServiceRequests { get; init; } = Array.Empty<ServiceRequestContext>();
        public IReadOnlyList<SpecimenContext> Specimens { get; init; } = Array.Empty<SpecimenContext>();
        public IReadOnlyList<EncounterContext> Encounters { get; init; } = Array.Empty<EncounterContext>();
        public DateTime MeasurementPeriodStart { get; init; } = DateTime.MinValue;
        public DateTime MeasurementPeriodEnd { get; init; } = DateTime.MaxValue;
        public IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> IpWindows { get; init; }
            = Array.Empty<MeasureInitialPopulationResolver.IpWindow>();
    }

    public sealed record EncounterContext(
        string EncounterId,
        DateTime PeriodStart,
        DateTime PeriodEnd,
        string ClassCode,
        string Status);

    public sealed record ConditionContext(
        string ResourceId,
        bool IsActive,
        DateTime RecordedDate,
        string EncounterReference,
        IReadOnlyList<string> CategoryCodes)
    {
        public DateTime OnsetStart { get; init; }
        public DateTime OnsetEnd { get; init; }

        public bool HasCategory(string code) =>
            CategoryCodes.Any(c => string.Equals(c, code, StringComparison.OrdinalIgnoreCase));
    }

    public sealed record ObservationContext(
        string ResourceId,
        string LoincCode,
        IReadOnlyList<string> CategoryCodes,
        DateTime EffectiveStart,
        DateTime EffectiveEnd)
    {
        public string Status { get; init; } = string.Empty;
        public string SpecimenReference { get; init; } = string.Empty;

        public bool HasCategory(string code) =>
            CategoryCodes.Any(c => string.Equals(c, code, StringComparison.OrdinalIgnoreCase));

        public bool OverlapsPeriod(DateTime periodStart, DateTime periodEnd) =>
            EffectiveStart <= periodEnd && EffectiveEnd >= periodStart;
    }

    public sealed record DiagnosticReportContext(
        string ResourceId,
        DateTime EffectiveStart,
        DateTime EffectiveEnd)
    {
        public IReadOnlyList<string> CategoryCodes { get; init; } = Array.Empty<string>();

        public bool OverlapsPeriod(DateTime periodStart, DateTime periodEnd) =>
            EffectiveStart <= periodEnd && EffectiveEnd >= periodStart;
    }

    public sealed record ProcedureContext(
        string ResourceId,
        DateTime PerformedStart,
        DateTime PerformedEnd,
        string EncounterReference)
    {
        public IReadOnlyList<string> Codes { get; init; } = Array.Empty<string>();

        public bool OverlapsPeriod(DateTime periodStart, DateTime periodEnd) =>
            PerformedStart <= periodEnd && PerformedEnd >= periodStart;
    }

    public sealed record MedicationRequestContext(
        string ResourceId,
        DateTime AuthoredOn,
        string EncounterReference)
    {
        public IReadOnlyList<string> MedicationCodes { get; init; } = Array.Empty<string>();
        public string Intent { get; init; } = string.Empty;
    }

    public sealed record MedicationAdministrationContext(
        string ResourceId,
        DateTime EffectiveStart,
        DateTime EffectiveEnd,
        string EncounterReference)
    {
        public IReadOnlyList<string> MedicationCodes { get; init; } = Array.Empty<string>();
        public string Status { get; init; } = string.Empty;

        public bool OverlapsPeriod(DateTime periodStart, DateTime periodEnd) =>
            EffectiveStart <= periodEnd && EffectiveEnd >= periodStart;
    }

    public sealed record CoverageContext(
        string ResourceId,
        DateTime PeriodStart,
        DateTime PeriodEnd)
    {
        public bool OverlapsPeriod(DateTime periodStart, DateTime periodEnd) =>
            PeriodStart <= periodEnd && PeriodEnd >= periodStart;
    }

    public sealed record ServiceRequestContext(
        string ResourceId,
        DateTime AuthoredOn,
        string EncounterReference)
    {
        public IReadOnlyList<string> Codes { get; init; } = Array.Empty<string>();
        public string Intent { get; init; } = string.Empty;
    }

    public sealed record SpecimenContext(
        string ResourceId,
        DateTime CollectionStart,
        DateTime CollectionEnd)
    {
        public string SubjectReference { get; init; } = string.Empty;

        public bool OverlapsPeriod(DateTime periodStart, DateTime periodEnd) =>
            CollectionStart <= periodEnd && CollectionEnd >= periodStart;
    }
}
