using System.Text.RegularExpressions;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Derives per-resource inclusion rules from a measure bundle's CQL SDE/population
/// defines. Used at generation time so ABS prediction follows the CQL that will
/// actually run in MeasureEval, not a frozen measure-family profile.
/// </summary>
internal static class CqlInstanceFilterAnalyzer
{
    private static readonly Regex RetrievePattern = new(
        """\[\s*([A-Z][A-Za-z]+)\s*(?:\]|:\s*(?:class\s+in\s+"([^"]+)"|class\s+~\s*"([^"]+)"|class\s+in\s*\{([^}]+)\}|"([^"]+)"))""",
        RegexOptions.Compiled);

    private static readonly Regex NamedDefineRefPattern = new(
        """^\s*"([^"]+)"(?:\s+[A-Za-z_][A-Za-z0-9_]*)?""",
        RegexOptions.Compiled);

    private static readonly Regex QuotedPattern = new("\"([^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex SingleQuotedPattern = new("'([^']+)'", RegexOptions.Compiled);
    private static readonly Regex StatusInPattern = new(
        """status\s+in\s*\{([^}]+)\}""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex StatusTildePattern = new(
        """status\s*~\s*'([^']+)'""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex IntentTildePattern = new(
        """intent\s*~\s*'([^']+)'""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    // Match each category tilde independently (`Category ~ "imaging"` or
    // `categories ~ "encounter-diagnosis"`). A greedy
    // `.category[\s\S]{0,160}~ "code"` span jumped from the first `.category`
    // to the last tilde in an `or` chain, so ACH Monthly SDE Observation
    // Category kept only `procedure` and dropped `imaging`.
    private static readonly Regex CategoryTildePattern = new(
        @"\bcategor(?:y|ies)\s*~\s*""([^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Dictionary<string, string> ReturnFunctionToType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ConditionResource"] = "Condition",
        ["ObservationLabResource"] = "Observation",
        ["ObservationVitalSignsResource"] = "Observation",
        ["ObservationResource"] = "Observation",
        ["EncounterResource"] = "Encounter",
        ["CoverageResource"] = "Coverage",
        ["ProcedureResource"] = "Procedure",
        ["MedicationRequestResource"] = "MedicationRequest",
        ["MedicationAdministrationResource"] = "MedicationAdministration",
        ["ServiceRequestResource"] = "ServiceRequest",
        ["SpecimenResource"] = "Specimen",
        ["DiagnosticReportLabResource"] = "DiagnosticReport",
        ["DiagnosticReportResource"] = "DiagnosticReport",
        ["GetLocation"] = "Location",
        ["LocationResource"] = "Location",
        ["DeviceResource"] = "Device",
        ["PatientResource"] = "Patient",
        ["MedicationResource"] = "Medication"
    };

    public sealed class MeasureFilterModel
    {
        public IReadOnlyList<CqlInclusionRule> Rules { get; init; } = [];
        public IReadOnlySet<string> IpClassCodes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlySet<string> IpStatusCodes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public bool IpAllowsAnyClass { get; init; }
    }

    public sealed class CqlInclusionRule
    {
        public string ResourceType { get; init; } = string.Empty;
        public HashSet<string>? CategoryAnyOf { get; init; }
        public HashSet<string>? CategoryNoneOf { get; init; }
        public HashSet<string>? StatusAnyOf { get; init; }
        public HashSet<string>? IntentAnyOf { get; init; }
        public HashSet<string>? CodeAnyOf { get; init; }
        public DateRelation Date { get; init; }
        public bool RequireIpExists { get; init; }
        public bool RequireEncounterLinkedToIp { get; init; }
        public bool MustBeIpEncounter { get; init; }
        public bool LocationFromIpEncounterLocations { get; init; }
        public bool SubjectMustBePatient { get; init; }
        public bool SpecimenFromMatchingObservations { get; init; }
        public List<CqlInclusionRule> ObservationSourceRules { get; init; } = [];
    }

    public enum DateRelation
    {
        None,
        OverlapsIpPeriod,
        DuringIpPeriod,
        StartDuringIpPeriod,
        DuringMeasurementPeriod,
        OverlapsMeasurementPeriod,
        CoverageActiveAtIpEnd
    }

    public static MeasureFilterModel Analyze(string bundleJson)
    {
        var model = CqlMeasureBundleModel.Parse(bundleJson);
        var reachable = model.ResolveReachableDefines();
        var rules = new List<CqlInclusionRule>();
        foreach (var name in reachable)
        {
            if (!model.RootExpressionNames.Contains(name))
                continue;
            // Population criteria (Initial Population) retrieve Encounters for membership.
            // ABS instance prediction follows SDE defines that return resources into contained.
            if (!name.StartsWith("SDE", StringComparison.OrdinalIgnoreCase))
                continue;
            rules.AddRange(AnalyzeDefine(model, name, new HashSet<string>(StringComparer.Ordinal)));
        }

        var (ipClasses, ipStatuses, ipAnyClass) = ExtractIpConstraints(model);
        return new MeasureFilterModel
        {
            Rules = rules,
            IpClassCodes = ipClasses,
            IpStatusCodes = ipStatuses,
            IpAllowsAnyClass = ipAnyClass
        };
    }

    private static List<CqlInclusionRule> AnalyzeDefine(
        CqlMeasureBundleModel model,
        string name,
        HashSet<string> visiting)
    {
        if (!visiting.Add(name))
            return [];
        if (!model.Defines.TryGetValue(name, out var body))
            return [];

        var parts = CqlText.SplitTopLevelUnion(CqlText.UnwrapOuterParens(body)).ToList();
        if (parts.Count > 1)
        {
            var unionRules = new List<CqlInclusionRule>();
            foreach (var part in parts)
                unionRules.AddRange(AnalyzeFragment(model, part, visiting));
            visiting.Remove(name);
            return unionRules;
        }

        var result = AnalyzeFragment(model, body, visiting);
        visiting.Remove(name);
        return result;
    }

    private static List<CqlInclusionRule> AnalyzeFragment(
        CqlMeasureBundleModel model,
        string fragment,
        HashSet<string> visiting)
    {
        var body = CqlText.UnwrapOuterParens(fragment);
        var whereClause = CqlText.ExtractTopLevelWhere(body);
        var returnClause = CqlText.ExtractTopLevelReturn(body);
        var predicates = ParseWhere(model, whereClause);
        if (LooksLikeLocationFromIpEncounter(body))
            predicates.LocationFromIpEncounter = true;
        var returnType = InferReturnType(returnClause);

        if (LooksLikeSpecimenFromObservation(body, returnClause)
            || string.Equals(returnType, "Specimen", StringComparison.OrdinalIgnoreCase)
               && body.Contains("GetSpecimen", StringComparison.OrdinalIgnoreCase))
        {
            var sourceName = FirstNamedDefineReference(body);
            var observationRules = sourceName != null
                ? AnalyzeDefine(model, sourceName, visiting)
                : [];
            return
            [
                new CqlInclusionRule
                {
                    ResourceType = "Specimen",
                    SubjectMustBePatient = true,
                    SpecimenFromMatchingObservations = true,
                    ObservationSourceRules = observationRules.Where(r =>
                        string.Equals(r.ResourceType, "Observation", StringComparison.OrdinalIgnoreCase)).ToList(),
                    RequireIpExists = predicates.RequireIpExists
                }
            ];
        }

        var named = FirstNamedDefineReference(body);
        if (named != null && model.Defines.ContainsKey(named))
        {
            if (string.Equals(named, "Initial Population", StringComparison.Ordinal)
                && string.Equals(returnType ?? "Encounter", "Encounter", StringComparison.OrdinalIgnoreCase))
            {
                return
                [
                    Merge(new CqlInclusionRule
                    {
                        ResourceType = "Encounter",
                        MustBeIpEncounter = true,
                        RequireIpExists = true
                    }, predicates, returnType)
                ];
            }

            var inherited = AnalyzeDefine(model, named, visiting);
            if (inherited.Count > 0)
                return inherited.Select(rule => Merge(rule, predicates, returnType)).ToList();
        }

        var rules = new List<CqlInclusionRule>();
        foreach (Match retrieve in RetrievePattern.Matches(body))
        {
            var resourceType = retrieve.Groups[1].Value;
            HashSet<string>? codes = null;
            var vsName = FirstNonEmpty(retrieve.Groups[2].Value, retrieve.Groups[5].Value);
            if (!string.IsNullOrWhiteSpace(vsName))
            {
                codes = model.CodesForValueSet(vsName)
                        ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "__unresolved-valueset__" };
            }

            var classTilde = retrieve.Groups[3].Value;
            if (!string.IsNullOrWhiteSpace(classTilde))
                codes = UnionCodes(codes, [model.ResolveCode(classTilde)]);

            var classSet = retrieve.Groups[4].Value;
            if (!string.IsNullOrWhiteSpace(classSet))
            {
                var setCodes = QuotedPattern.Matches(classSet)
                    .Select(m => model.ResolveCode(m.Groups[1].Value));
                codes = UnionCodes(codes, setCodes);
            }

            rules.Add(Merge(new CqlInclusionRule
            {
                ResourceType = resourceType,
                CodeAnyOf = codes,
                SubjectMustBePatient = string.Equals(resourceType, "Specimen", StringComparison.OrdinalIgnoreCase)
            }, predicates, returnType ?? resourceType));
        }

        if (rules.Count == 0 && !string.IsNullOrWhiteSpace(returnType))
        {
            rules.Add(Merge(new CqlInclusionRule
            {
                ResourceType = returnType,
                SubjectMustBePatient = string.Equals(returnType, "Specimen", StringComparison.OrdinalIgnoreCase)
            }, predicates, returnType));
        }

        return rules;
    }

    private static CqlInclusionRule Merge(CqlInclusionRule source, WherePredicates predicates, string? returnType)
    {
        return new CqlInclusionRule
        {
            ResourceType = returnType ?? source.ResourceType,
            CategoryAnyOf = IntersectOrReplace(source.CategoryAnyOf, predicates.CategoryAnyOf),
            CategoryNoneOf = UnionOrReplace(source.CategoryNoneOf, predicates.CategoryNoneOf),
            StatusAnyOf = IntersectOrReplace(source.StatusAnyOf, predicates.StatusAnyOf),
            IntentAnyOf = IntersectOrReplace(source.IntentAnyOf, predicates.IntentAnyOf),
            CodeAnyOf = IntersectOrReplace(source.CodeAnyOf, predicates.CodeAnyOf),
            Date = predicates.Date != DateRelation.None ? predicates.Date : source.Date,
            RequireIpExists = source.RequireIpExists || predicates.RequireIpExists,
            RequireEncounterLinkedToIp = source.RequireEncounterLinkedToIp || predicates.RequireEncounterLinkedToIp,
            MustBeIpEncounter = source.MustBeIpEncounter,
            LocationFromIpEncounterLocations = source.LocationFromIpEncounterLocations || predicates.LocationFromIpEncounter,
            SubjectMustBePatient = source.SubjectMustBePatient,
            SpecimenFromMatchingObservations = source.SpecimenFromMatchingObservations,
            ObservationSourceRules = source.ObservationSourceRules
        };
    }

    private static HashSet<string>? IntersectOrReplace(HashSet<string>? inherited, HashSet<string>? extra)
    {
        if (extra == null || extra.Count == 0)
            return inherited;
        if (inherited == null || inherited.Count == 0)
            return extra;
        var merged = new HashSet<string>(inherited, StringComparer.OrdinalIgnoreCase);
        merged.IntersectWith(extra);
        return merged.Count == 0 ? extra : merged;
    }

    private static HashSet<string>? UnionOrReplace(HashSet<string>? inherited, HashSet<string>? extra)
    {
        if (extra == null || extra.Count == 0)
            return inherited;
        if (inherited == null || inherited.Count == 0)
            return extra;
        var merged = new HashSet<string>(inherited, StringComparer.OrdinalIgnoreCase);
        merged.UnionWith(extra);
        return merged;
    }

    private static HashSet<string>? UnionCodes(HashSet<string>? existing, IEnumerable<string> extra)
    {
        var set = existing ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in extra)
        {
            if (!string.IsNullOrWhiteSpace(code))
                set.Add(code);
        }

        return set.Count == 0 ? existing : set;
    }

    private sealed class WherePredicates
    {
        public HashSet<string>? CategoryAnyOf { get; set; }
        public HashSet<string>? CategoryNoneOf { get; set; }
        public HashSet<string>? StatusAnyOf { get; set; }
        public HashSet<string>? IntentAnyOf { get; set; }
        public HashSet<string>? CodeAnyOf { get; set; }
        public DateRelation Date { get; set; }
        public bool RequireIpExists { get; set; }
        public bool RequireEncounterLinkedToIp { get; set; }
        public bool LocationFromIpEncounter { get; set; }
    }

    private static WherePredicates ParseWhere(CqlMeasureBundleModel model, string? whereClause)
    {
        var result = new WherePredicates();
        if (string.IsNullOrWhiteSpace(whereClause))
            return result;

        var text = whereClause;
        if (Regex.IsMatch(text, """exists\s*\(\s*"Initial Population" """, RegexOptions.IgnoreCase)
            || Regex.IsMatch(text, """exists\s*\(\s*"Initial Population"\s*\)""", RegexOptions.IgnoreCase)
            || text.Contains("\"Initial Population\"", StringComparison.Ordinal))
        {
            result.RequireIpExists = true;
        }

        if (text.Contains("encounter.reference", StringComparison.OrdinalIgnoreCase)
            || text.Contains(".diagnosis", StringComparison.OrdinalIgnoreCase))
        {
            result.RequireEncounterLinkedToIp = true;
        }

        var categoryAnyOf = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var categoryNoneOf = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var notSpans = FindNotSpans(text);
        foreach (Match match in CategoryTildePattern.Matches(text))
        {
            var code = model.ResolveCode(match.Groups[1].Value);
            if (notSpans.Any(span => match.Index >= span.Start && match.Index <= span.End))
                categoryNoneOf.Add(code);
            else
                categoryAnyOf.Add(code);
        }

        if (categoryAnyOf.Count > 0)
            result.CategoryAnyOf = categoryAnyOf;
        if (categoryNoneOf.Count > 0)
            result.CategoryNoneOf = categoryNoneOf;

        var statusIn = StatusInPattern.Match(text);
        if (statusIn.Success)
        {
            result.StatusAnyOf = SingleQuotedPattern.Matches(statusIn.Groups[1].Value)
                .Select(m => m.Groups[1].Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            var statusTilde = StatusTildePattern.Match(text);
            if (statusTilde.Success)
                result.StatusAnyOf = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { statusTilde.Groups[1].Value };
        }

        var intent = IntentTildePattern.Match(text);
        if (intent.Success)
            result.IntentAnyOf = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { intent.Groups[1].Value };

        result.Date = InferDateRelation(text);

        foreach (Match match in Regex.Matches(text, @"\bin\s+""([^""]+)""", RegexOptions.IgnoreCase))
        {
            var vsName = match.Groups[1].Value;
            if (string.Equals(vsName, "Initial Population", StringComparison.Ordinal)
                || string.Equals(vsName, "Measurement Period", StringComparison.Ordinal)
                || vsName.Contains("Hospitalization", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var codes = model.CodesForValueSet(vsName);
            if (codes is not { Count: > 0 })
                continue;
            result.CodeAnyOf ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in codes)
                result.CodeAnyOf.Add(code);
        }

        return result;
    }

    private static DateRelation InferDateRelation(string text)
    {
        var ipPeriod = text.Contains("IP.period", StringComparison.OrdinalIgnoreCase)
                       || text.Contains("InitialPopulation.period", StringComparison.OrdinalIgnoreCase)
                       || text.Contains("InpatientEncounters.period", StringComparison.OrdinalIgnoreCase);

        if (Regex.IsMatch(text, """start of .+period before""", RegexOptions.IgnoreCase)
            && text.Contains("Coverages.period", StringComparison.OrdinalIgnoreCase))
        {
            return DateRelation.CoverageActiveAtIpEnd;
        }

        if (text.Contains("\"Measurement Period\"", StringComparison.Ordinal))
        {
            return text.Contains("overlaps", StringComparison.OrdinalIgnoreCase)
                ? DateRelation.OverlapsMeasurementPeriod
                : DateRelation.DuringMeasurementPeriod;
        }

        if (!ipPeriod && !text.Contains("HospitalizationWithObservationOrEmergency", StringComparison.Ordinal))
            return DateRelation.None;

        if (Regex.IsMatch(text, """start of .+ during""", RegexOptions.IgnoreCase))
            return DateRelation.StartDuringIpPeriod;
        if (text.Contains("overlaps", StringComparison.OrdinalIgnoreCase))
            return DateRelation.OverlapsIpPeriod;
        if (text.Contains("during", StringComparison.OrdinalIgnoreCase))
            return DateRelation.DuringIpPeriod;
        return DateRelation.None;
    }

    private static string? InferReturnType(string? returnClause)
    {
        if (string.IsNullOrWhiteSpace(returnClause))
            return null;
        foreach (var (fn, type) in ReturnFunctionToType)
        {
            if (returnClause.Contains(fn, StringComparison.OrdinalIgnoreCase))
                return type;
        }

        return null;
    }

    private static bool LooksLikeLocationFromIpEncounter(string body)
    {
        if (body.Contains("GetLocation", StringComparison.OrdinalIgnoreCase)
            && (body.Contains("IP.location", StringComparison.Ordinal)
                || body.Contains("locationElements", StringComparison.OrdinalIgnoreCase)
                || body.Contains("InitialPopulation.location", StringComparison.OrdinalIgnoreCase)
                || (body.Contains("\"Initial Population\"", StringComparison.Ordinal)
                    && body.Contains(".location", StringComparison.OrdinalIgnoreCase))))
        {
            return true;
        }

        return Regex.IsMatch(
            body,
            @"Get Locations from (IP|Initial Population)",
            RegexOptions.IgnoreCase);
    }

    private static List<(int Start, int End)> FindNotSpans(string text)
    {
        var spans = new List<(int Start, int End)>();
        for (var i = 0; i < text.Length; i++)
        {
            if (!CqlText.IsKeywordAt(text, i, "not"))
                continue;

            var j = i + 3;
            while (j < text.Length && char.IsWhiteSpace(text[j]))
                j++;
            if (j >= text.Length || text[j] != '(')
                continue;

            var end = MatchingCloseParen(text, j);
            if (end > j)
                spans.Add((j, end));
            i = j;
        }

        return spans;
    }

    private static int MatchingCloseParen(string text, int openIndex)
    {
        var depth = 0;
        for (var i = openIndex; i < text.Length; i++)
        {
            if (text[i] == '(')
                depth++;
            else if (text[i] == ')')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return -1;
    }

    private static bool LooksLikeSpecimenFromObservation(string body, string? returnClause)
        => (body.Contains("SpecimenResource", StringComparison.OrdinalIgnoreCase)
            || (returnClause ?? string.Empty).Contains("SpecimenResource", StringComparison.OrdinalIgnoreCase))
           && (body.Contains("GetSpecimen", StringComparison.OrdinalIgnoreCase)
               || body.Contains(".specimen", StringComparison.OrdinalIgnoreCase));

    private static string? FirstNamedDefineReference(string body)
    {
        var trimmed = CqlText.UnwrapOuterParens(body).TrimStart();
        var match = NamedDefineRefPattern.Match(trimmed);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static (HashSet<string> Classes, HashSet<string> Statuses, bool AnyClass) ExtractIpConstraints(
        CqlMeasureBundleModel model)
    {
        var classes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var statuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var anyClass = false;
        if (!model.Defines.TryGetValue("Initial Population", out var ipBody))
            return (classes, statuses, true);

        var stack = new Stack<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal) { "Initial Population" };
        stack.Push(ipBody);
        foreach (Match quoted in QuotedPattern.Matches(ipBody))
        {
            if (seen.Add(quoted.Groups[1].Value) && model.Defines.TryGetValue(quoted.Groups[1].Value, out var nested))
                stack.Push(nested);
        }

        while (stack.Count > 0)
        {
            var body = stack.Pop();
            foreach (Match quoted in QuotedPattern.Matches(body))
            {
                if (seen.Add(quoted.Groups[1].Value) && model.Defines.TryGetValue(quoted.Groups[1].Value, out var nested))
                    stack.Push(nested);
            }

            foreach (Match retrieve in RetrievePattern.Matches(body))
            {
                if (!string.Equals(retrieve.Groups[1].Value, "Encounter", StringComparison.OrdinalIgnoreCase))
                    continue;

                var vs = FirstNonEmpty(retrieve.Groups[2].Value, retrieve.Groups[5].Value);
                var classTilde = retrieve.Groups[3].Value;
                var classSet = retrieve.Groups[4].Value;
                if (!string.IsNullOrWhiteSpace(vs) && vs.Contains("Class", StringComparison.OrdinalIgnoreCase))
                {
                    var codes = model.CodesForValueSet(vs);
                    if (codes != null)
                    {
                        foreach (var code in codes) classes.Add(code);
                    }
                }

                if (!string.IsNullOrWhiteSpace(classTilde))
                    classes.Add(model.ResolveCode(classTilde));

                if (!string.IsNullOrWhiteSpace(classSet))
                {
                    foreach (Match quoted in QuotedPattern.Matches(classSet))
                        classes.Add(model.ResolveCode(quoted.Groups[1].Value));
                }

                // Bare [Encounter] and encounter-type valuesets (Encounter Inpatient, ED Visit)
                // are not class-code filters. EncounterContext does not carry type/location, so
                // those IP paths stay a documented gap rather than treating every class as IP.
            }

            var statusIn = StatusInPattern.Match(body);
            if (statusIn.Success)
            {
                foreach (Match quoted in SingleQuotedPattern.Matches(statusIn.Groups[1].Value))
                    statuses.Add(quoted.Groups[1].Value);
            }
        }

        if (classes.Count == 0)
            anyClass = true;
        return (classes, statuses, anyClass);
    }
}
