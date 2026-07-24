namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Simulates measure-specific CQL SDE <c>where</c>-clause filtering at the individual-resource level.
///
/// Type-level reachability (<c>[ResourceType]</c>) is handled elsewhere. This simulator focuses on
/// per-resource exclusions where CQL retrieves a type but includes only rows matching additional
/// predicates (status/date/category/reference constraints).
///
/// Operates on <b>actual extracted resource attributes</b> (via <see cref="CqlFilterInputExtractor"/>)
/// rather than seed replay, so it stays accurate even if generator internals drift.
/// </summary>
public static class CqlFilterSimulator
{
    private static readonly IReadOnlyList<ICqlFilterProfile> Profiles =
    [
        new AchConditionFilterProfile(),
        new HypoglycemicConditionFilterProfile(),
        new AchObservationFilterProfile(),
        new HypoglycemicObservationFilterProfile(),
        new AchDiagnosticReportFilterProfile(),
        new AchProcedureFilterProfile(),
        new AchHypoMedicationRequestFilterProfile(),
        new HypoMedicationAdministrationFilterProfile(),
        new AchHypoCoverageFilterProfile(),
        new AchHypoServiceRequestFilterProfile(),
        new AchEncounterFilterProfile(),
        new AchMonthlySpecimenFilterProfile(),
        new AchDailySpecimenFilterProfile(),
        new HypoglycemicSpecimenFilterProfile()
    ];

    /// <summary>
    /// Computes resource keys CQL SDE <c>where</c> clauses will exclude for the patient.
    ///
    /// The intersection rule is applied <b>per resource type</b>: a key is excluded only when
    /// every applicable profile that targets that resource type excludes it. Profiles for
    /// other resource types do not participate in that intersection — an Observation profile
    /// has no opinion about whether a Condition belongs in ABS, and vice-versa.
    ///
    /// MeasureEval evaluates each measure independently and writes one <c>.mr</c> file per
    /// measure; PatientAggregator unions the contained resources across those files when
    /// producing the patient NDJSON. So if any one applicable measure includes the resource,
    /// it appears in ABS regardless of the others.
    /// </summary>
    public static HashSet<string> ComputeFilteredKeys(
        IReadOnlyList<ProfiledMeasureType> measures,
        PatientCqlInput input)
    {
        if (measures == null || measures.Count == 0 || input == null)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Resolve the union of IP windows across all selected (qualifying) measures. Each
        // profile then queries this set via IpWindowExtensions to model the CQL pattern
        // "where exists IP InitialPopulation where Resource.X overlaps IP.period".
        var ipWindows = MeasureInitialPopulationResolver.Resolve(measures, input);

        // Back-compat: if the input was built via the legacy single-encounter positional
        // ctor (no Encounters list), fall back to a single window from the EncounterId/
        // Start/End triple. Pre-Landing-3 tests and the synthetic single-encounter
        // generator path both rely on this.
        if (ipWindows.Count == 0
            && (input.Encounters == null || input.Encounters.Count == 0)
            && !string.IsNullOrEmpty(input.EncounterId))
        {
            ipWindows = [new MeasureInitialPopulationResolver.IpWindow(input.EncounterId, input.EncounterStart, input.EncounterEnd)];
        }

        var enriched = input with { IpWindows = ipWindows };

        var perTypeExclusions = new Dictionary<string, List<HashSet<string>>>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in Profiles)
        {
            if (!profile.AppliesToAny(measures))
                continue;

            if (!perTypeExclusions.TryGetValue(profile.TargetResourceType, out var bucket))
            {
                bucket = new List<HashSet<string>>();
                perTypeExclusions[profile.TargetResourceType] = bucket;
            }
            bucket.Add(profile.ComputeExcludedKeys(enriched));
        }

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var bucket in perTypeExclusions.Values)
        {
            // Intersect within a resource type: keep keys every applicable profile of that
            // type excludes. Then union across types into the final excluded-key set.
            var intersection = new HashSet<string>(bucket[0], StringComparer.OrdinalIgnoreCase);
            for (var i = 1; i < bucket.Count; i++)
                intersection.IntersectWith(bucket[i]);

            foreach (var key in intersection)
                result.Add(key);
        }

        return result;
    }

    /// <summary>
    /// Extracted per-patient inputs used by the simulator.
    /// Build via <see cref="CqlFilterInputExtractor"/>.
    ///
    /// New resource-type collections (Procedures / MedicationRequests / etc.) and
    /// multi-encounter / measurement-period fields are exposed as <c>init</c> properties
    /// with sensible defaults so older callers / tests using only the positional ctor for
    /// Conditions + Observations continue to compile and behave the same.
    ///
    /// <list type="bullet">
    ///   <item><see cref="Encounters"/> — ALL encounters extracted from the patient's data. The simulator's IP resolver consumes this list.</item>
    ///   <item><see cref="MeasurementPeriodStart"/> / <see cref="MeasurementPeriodEnd"/> — when set, IP encounters must overlap this window. Default is open (MinValue/MaxValue) for back-compat.</item>
    ///   <item><see cref="IpWindows"/> — populated by <see cref="ComputeFilteredKeys"/> before profiles run; never set by the extractor.</item>
    /// </list>
    /// </summary>
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

    /// <summary>
    /// CQL-relevant attributes of a patient's encounter. <see cref="ClassCode"/> is the
    /// FHIR <c>class.code</c> ('IMP', 'EMER', 'AMB', etc.) used by the IP resolver to
    /// decide which encounters constitute the Initial Population for each measure.
    /// </summary>
    public sealed record EncounterContext(
        string EncounterId,
        DateTime PeriodStart,
        DateTime PeriodEnd,
        string ClassCode,
        string Status);

    public interface ICqlFilterProfile
    {
        /// <summary>
        /// FHIR resource type this profile produces exclusions for (e.g. <c>Condition</c>,
        /// <c>Observation</c>). Used by <see cref="ComputeFilteredKeys"/> to scope the
        /// intersection rule to profiles that operate on the same resource type.
        /// </summary>
        string TargetResourceType { get; }

        bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures);
        HashSet<string> ComputeExcludedKeys(PatientCqlInput input);
    }

    private abstract class ConditionFilterProfileBase : ICqlFilterProfile
    {
        public string TargetResourceType => "Condition";

        public abstract bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures);

        protected abstract bool IncludeCondition(
            ConditionContext c,
            IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> ipWindows);

        public HashSet<string> ComputeExcludedKeys(PatientCqlInput input)
        {
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in input.Conditions)
            {
                if (!IncludeCondition(c, input.IpWindows))
                    excluded.Add($"Condition/{c.ResourceId}");
            }
            return excluded;
        }
    }

    /// <summary>
    /// ACH Monthly + ACH Daily SDE Condition semantics:
    /// - problem-list-item requires active + recordedDate strictly before the end of any IP encounter
    /// - OR encounter-diagnosis/health-concern tied to ANY IP encounter
    /// </summary>
    private sealed class AchConditionFilterProfile : ConditionFilterProfileBase
    {
        public override bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures) =>
            measures.Contains(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)
            || measures.Contains(ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation);

        protected override bool IncludeCondition(ConditionContext c, IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> ipWindows)
        {
            if (c.HasCategory("problem-list-item") && c.IsActive
                && ipWindows.AnyEndStrictlyAfter(c.RecordedDate))
            {
                return true;
            }

            if ((c.HasCategory("encounter-diagnosis") || c.HasCategory("health-concern"))
                && ipWindows.AnyEncounterMatches(c.EncounterReference))
            {
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Hypoglycemic SDE Condition semantics:
    /// - conditions whose recordedDate is on or before the end of ANY IP encounter are included
    /// - no active-status constraint in this measure's SDE Condition define.
    /// </summary>
    private sealed class HypoglycemicConditionFilterProfile : ConditionFilterProfileBase
    {
        public override bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures) =>
            measures.Contains(ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation);

        protected override bool IncludeCondition(ConditionContext c, IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> ipWindows)
        {
            return ipWindows.AnyEndOnOrAfter(c.RecordedDate);
        }
    }

    /// <summary>
    /// CQL-relevant attributes of a generated Condition resource.
    /// Extracted from the actual generated FHIR content (no seed replay).
    /// </summary>
    public sealed record ConditionContext(
        string ResourceId,
        bool IsActive,
        DateTime RecordedDate,
        string EncounterReference,
        IReadOnlyList<string> CategoryCodes)
    {
        public bool HasCategory(string code) =>
            CategoryCodes.Any(c => string.Equals(c, code, StringComparison.OrdinalIgnoreCase));
    }

    // -------------------------------------------------------------------
    //  Observation profiles
    // -------------------------------------------------------------------

    /// <summary>
    /// CQL-relevant attributes of a generated Observation resource.
    /// Extracted from the actual generated FHIR content (no seed replay).
    /// <see cref="EffectiveStart"/> / <see cref="EffectiveEnd"/> normalize both
    /// <c>effectiveDateTime</c> (start == end) and <c>effectivePeriod</c> shapes.
    /// </summary>
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

        /// <summary>
        /// True when the observation's effective range overlaps the closed interval
        /// [periodStart, periodEnd] using FHIR "overlaps" semantics.
        /// </summary>
        public bool OverlapsPeriod(DateTime periodStart, DateTime periodEnd) =>
            EffectiveStart <= periodEnd && EffectiveEnd >= periodStart;
    }

    private abstract class ObservationFilterProfileBase : ICqlFilterProfile
    {
        public string TargetResourceType => "Observation";

        public abstract bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures);

        protected abstract bool IncludeObservation(
            ObservationContext o,
            IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> ipWindows);

        public HashSet<string> ComputeExcludedKeys(PatientCqlInput input)
        {
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in input.Observations)
            {
                if (!IncludeObservation(o, input.IpWindows))
                    excluded.Add($"Observation/{o.ResourceId}");
            }
            return excluded;
        }
    }

    /// <summary>
    /// ACH Monthly + ACH Daily SDE Observation semantics:
    /// <list type="bullet">
    ///   <item>SDE Observation Lab Category: <c>category ~ "laboratory"</c> AND effective overlaps IP.</item>
    ///   <item>SDE Observation Vital Signs Category: <c>category ~ "vital-signs"</c> AND effective overlaps IP.</item>
    ///   <item>SDE Observation Category (catch-all): <c>category</c> in
    ///         <c>{social-history, survey, imaging, procedure}</c> AND effective overlaps IP.</item>
    /// </list>
    /// IP for the simulator is approximated by the patient's encounter period, which is
    /// how the generator places observations and how the measures' Initial Population
    /// resolves for the synthetic patients (one qualifying encounter per patient).
    /// </summary>
    private sealed class AchObservationFilterProfile : ObservationFilterProfileBase
    {
        private static readonly HashSet<string> AchCategoryCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "laboratory",
            "vital-signs",
            "social-history",
            "survey",
            "imaging",
            "procedure"
        };

        public override bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures) =>
            measures.Contains(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)
            || measures.Contains(ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation);

        protected override bool IncludeObservation(ObservationContext o, IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> ipWindows)
        {
            if (!o.CategoryCodes.Any(c => AchCategoryCodes.Contains(c)))
                return false;

            return ipWindows.AnyOverlaps(o.EffectiveStart, o.EffectiveEnd);
        }
    }

    /// <summary>
    /// Hypoglycemic SDE Observation semantics:
    /// <c>[Observation: "Blood Glucose Laboratory and Point of Care Tests"]</c> retrieve,
    /// then <c>start of effective during InitialPopulation period</c>.
    ///
    /// Only blood-glucose lab/POC LOINCs are reachable; every other observation is dropped
    /// by the value-set bound retrieve regardless of category or effective date.
    /// The whitelist below is the subset of the measure's value set that the synthetic
    /// generator currently emits; expanding the generator pool is the only thing that
    /// would require expanding this whitelist.
    /// </summary>
    private sealed class HypoglycemicObservationFilterProfile : ObservationFilterProfileBase
    {
        private static readonly HashSet<string> BloodGlucoseLoincCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "2339-0",   // Glucose [Mass/volume] in Blood
            "2345-7",   // Glucose [Mass/volume] in Serum or Plasma
            "41653-7"   // Glucose [Mass/volume] in Capillary blood by Glucometer
        };

        public override bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures) =>
            measures.Contains(ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation);

        protected override bool IncludeObservation(ObservationContext o, IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> ipWindows)
        {
            if (!BloodGlucoseLoincCodes.Contains(o.LoincCode))
                return false;

            // Hypoglycemic uses "start of effective during IP" — point-in-IP semantics.
            return ipWindows.AnyContains(o.EffectiveStart);
        }
    }

    // -------------------------------------------------------------------
    //  DiagnosticReport profiles
    // -------------------------------------------------------------------

    /// <summary>
    /// CQL-relevant attributes of a generated DiagnosticReport resource.
    /// Effective start/end normalize both effectiveDateTime and effectivePeriod.
    /// </summary>
    public sealed record DiagnosticReportContext(
        string ResourceId,
        DateTime EffectiveStart,
        DateTime EffectiveEnd)
    {
        public bool OverlapsPeriod(DateTime periodStart, DateTime periodEnd) =>
            EffectiveStart <= periodEnd && EffectiveEnd >= periodStart;
    }

    private abstract class DiagnosticReportFilterProfileBase : ICqlFilterProfile
    {
        public string TargetResourceType => "DiagnosticReport";
        public abstract bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures);
        protected abstract bool IncludeDiagnosticReport(DiagnosticReportContext d, IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> ipWindows);

        public HashSet<string> ComputeExcludedKeys(PatientCqlInput input)
        {
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in input.DiagnosticReports)
            {
                if (!IncludeDiagnosticReport(d, input.IpWindows))
                    excluded.Add($"DiagnosticReport/{d.ResourceId}");
            }
            return excluded;
        }
    }

    /// <summary>
    /// ACH Monthly + Daily SDE DiagnosticReport semantics:
    /// include reports whose effective period overlaps any Initial Population encounter window.
    /// </summary>
    private sealed class AchDiagnosticReportFilterProfile : DiagnosticReportFilterProfileBase
    {
        public override bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures) =>
            measures.Contains(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)
            || measures.Contains(ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation);

        protected override bool IncludeDiagnosticReport(DiagnosticReportContext d, IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> ipWindows) =>
            ipWindows.AnyOverlaps(d.EffectiveStart, d.EffectiveEnd);
    }

    // -------------------------------------------------------------------
    //  Procedure / MedicationRequest / MedicationAdministration / Coverage / ServiceRequest
    //
    //  These resource types are retrieved by SDE definitions in both ACH and Hypoglycemic
    //  measures and are filtered against the Initial Population (IP) encounter's period.
    //  Each profile iterates the resolved IpWindows, modeling the CQL pattern
    //  "where exists IP InitialPopulation where Resource.X overlaps IP.period".
    // -------------------------------------------------------------------

    /// <summary>
    /// CQL-relevant attributes of a Procedure resource. Performed start/end normalize both
    /// <c>performedDateTime</c> (start == end) and <c>performedPeriod</c> shapes.
    /// </summary>
    public sealed record ProcedureContext(
        string ResourceId,
        DateTime PerformedStart,
        DateTime PerformedEnd,
        string EncounterReference)
    {
        public bool OverlapsPeriod(DateTime periodStart, DateTime periodEnd) =>
            PerformedStart <= periodEnd && PerformedEnd >= periodStart;
    }

    /// <summary>CQL-relevant attributes of a MedicationRequest. <c>authoredOn</c> is the only date.</summary>
    public sealed record MedicationRequestContext(
        string ResourceId,
        DateTime AuthoredOn,
        string EncounterReference);

    /// <summary>CQL-relevant attributes of a MedicationAdministration. <c>effective</c> normalizes both DateTime and Period shapes.</summary>
    public sealed record MedicationAdministrationContext(
        string ResourceId,
        DateTime EffectiveStart,
        DateTime EffectiveEnd,
        string EncounterReference)
    {
        public bool OverlapsPeriod(DateTime periodStart, DateTime periodEnd) =>
            EffectiveStart <= periodEnd && EffectiveEnd >= periodStart;
    }

    /// <summary>CQL-relevant attributes of a Coverage. <c>period</c> normalizes start/end with open-end support.</summary>
    public sealed record CoverageContext(
        string ResourceId,
        DateTime PeriodStart,
        DateTime PeriodEnd)
    {
        public bool OverlapsPeriod(DateTime periodStart, DateTime periodEnd) =>
            PeriodStart <= periodEnd && PeriodEnd >= periodStart;
    }

    /// <summary>CQL-relevant attributes of a ServiceRequest. <c>authoredOn</c> is the only date.</summary>
    public sealed record ServiceRequestContext(
        string ResourceId,
        DateTime AuthoredOn,
        string EncounterReference);

    /// <summary>
    /// CQL-relevant attributes of a Specimen. <c>collection.collected</c> normalizes both
    /// DateTime and Period shapes.
    /// </summary>
    public sealed record SpecimenContext(
        string ResourceId,
        DateTime CollectionStart,
        DateTime CollectionEnd)
    {
        public string SubjectReference { get; init; } = string.Empty;

        public bool OverlapsPeriod(DateTime periodStart, DateTime periodEnd) =>
            CollectionStart <= periodEnd && CollectionEnd >= periodStart;
    }

    // ----- Procedure profiles -----

    private abstract class ProcedureFilterProfileBase : ICqlFilterProfile
    {
        public string TargetResourceType => "Procedure";
        public abstract bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures);
        protected abstract bool IncludeProcedure(ProcedureContext p, IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> ipWindows);

        public HashSet<string> ComputeExcludedKeys(PatientCqlInput input)
        {
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in input.Procedures)
            {
                if (!IncludeProcedure(p, input.IpWindows))
                    excluded.Add($"Procedure/{p.ResourceId}");
            }
            return excluded;
        }
    }

    /// <summary>
    /// ACH Monthly + Daily SDE Procedure: <c>Procedures.performed overlaps IP.period</c>
    /// (overlap against ANY IP encounter window).
    /// </summary>
    private sealed class AchProcedureFilterProfile : ProcedureFilterProfileBase
    {
        public override bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures) =>
            measures.Contains(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)
            || measures.Contains(ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation);

        protected override bool IncludeProcedure(ProcedureContext p, IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> ipWindows) =>
            ipWindows.AnyOverlaps(p.PerformedStart, p.PerformedEnd);
    }

    // ----- MedicationRequest profiles -----

    private abstract class MedicationRequestFilterProfileBase : ICqlFilterProfile
    {
        public string TargetResourceType => "MedicationRequest";
        public abstract bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures);
        protected abstract bool IncludeMedicationRequest(MedicationRequestContext m, IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> ipWindows);

        public HashSet<string> ComputeExcludedKeys(PatientCqlInput input)
        {
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in input.MedicationRequests)
            {
                if (!IncludeMedicationRequest(m, input.IpWindows))
                    excluded.Add($"MedicationRequest/{m.ResourceId}");
            }
            return excluded;
        }
    }

    /// <summary>
    /// ACH Monthly + Daily + Hypoglycemic SDE Medication Request:
    /// <c>MedicationRequests.authoredOn during IP.period</c> (during ANY IP encounter window).
    /// (Hypoglycemic actually uses <c>HospitalizationWithObservationOrEmergency(IP)</c> which
    /// extends the window to include adjacent observation/ED encounters; approximated here
    /// by relying on the union of IP windows the resolver returns.)
    /// </summary>
    private sealed class AchHypoMedicationRequestFilterProfile : MedicationRequestFilterProfileBase
    {
        public override bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures) =>
            measures.Contains(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)
            || measures.Contains(ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation)
            || measures.Contains(ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation);

        protected override bool IncludeMedicationRequest(MedicationRequestContext m, IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> ipWindows) =>
            ipWindows.AnyContains(m.AuthoredOn);
    }

    // ----- MedicationAdministration profiles -----

    private abstract class MedicationAdministrationFilterProfileBase : ICqlFilterProfile
    {
        public string TargetResourceType => "MedicationAdministration";
        public abstract bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures);
        protected abstract bool IncludeMedicationAdministration(MedicationAdministrationContext m, IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> ipWindows);

        public HashSet<string> ComputeExcludedKeys(PatientCqlInput input)
        {
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in input.MedicationAdministrations)
            {
                if (!IncludeMedicationAdministration(m, input.IpWindows))
                    excluded.Add($"MedicationAdministration/{m.ResourceId}");
            }
            return excluded;
        }
    }

    /// <summary>
    /// Hypoglycemic SDE Medication Administration:
    /// <c>MedicationAdministrations.effective overlaps HospitalizationWithObservationOrEmergency(IP)</c>.
    /// Approximated as overlap with ANY IP encounter window. ACH measures do not retrieve
    /// MedicationAdministration directly.
    /// </summary>
    private sealed class HypoMedicationAdministrationFilterProfile : MedicationAdministrationFilterProfileBase
    {
        public override bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures) =>
            measures.Contains(ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation);

        protected override bool IncludeMedicationAdministration(MedicationAdministrationContext m, IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> ipWindows) =>
            ipWindows.AnyOverlaps(m.EffectiveStart, m.EffectiveEnd);
    }

    // ----- Coverage profiles -----

    private abstract class CoverageFilterProfileBase : ICqlFilterProfile
    {
        public string TargetResourceType => "Coverage";
        public abstract bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures);
        protected abstract bool IncludeCoverage(CoverageContext c, IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> ipWindows);

        public HashSet<string> ComputeExcludedKeys(PatientCqlInput input)
        {
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in input.Coverages)
            {
                if (!IncludeCoverage(c, input.IpWindows))
                    excluded.Add($"Coverage/{c.ResourceId}");
            }
            return excluded;
        }
    }

    /// <summary>
    /// ACH Monthly + Daily + Hypoglycemic SDE Coverage:
    /// <c>Coverages.period overlaps IP.period</c> (overlap against ANY IP encounter window).
    /// </summary>
    private sealed class AchHypoCoverageFilterProfile : CoverageFilterProfileBase
    {
        public override bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures) =>
            measures.Contains(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)
            || measures.Contains(ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation)
            || measures.Contains(ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation);

        protected override bool IncludeCoverage(CoverageContext c, IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> ipWindows) =>
            ipWindows.AnyOverlaps(c.PeriodStart, c.PeriodEnd);
    }

    // ----- ServiceRequest profiles -----

    private abstract class ServiceRequestFilterProfileBase : ICqlFilterProfile
    {
        public string TargetResourceType => "ServiceRequest";
        public abstract bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures);
        protected abstract bool IncludeServiceRequest(ServiceRequestContext s, IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> ipWindows);

        public HashSet<string> ComputeExcludedKeys(PatientCqlInput input)
        {
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in input.ServiceRequests)
            {
                if (!IncludeServiceRequest(s, input.IpWindows))
                    excluded.Add($"ServiceRequest/{s.ResourceId}");
            }
            return excluded;
        }
    }

    /// <summary>
    /// ACH Monthly + Daily + Hypoglycemic SDE Service Request:
    /// <c>ServiceRequests.authoredOn during IP.period</c> (during ANY IP encounter window).
    /// </summary>
    private sealed class AchHypoServiceRequestFilterProfile : ServiceRequestFilterProfileBase
    {
        public override bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures) =>
            measures.Contains(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)
            || measures.Contains(ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation)
            || measures.Contains(ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation);

        protected override bool IncludeServiceRequest(ServiceRequestContext s, IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> ipWindows) =>
            ipWindows.AnyContains(s.AuthoredOn);
    }

    // ----- Encounter profile -----

    private abstract class EncounterFilterProfileBase : ICqlFilterProfile
    {
        public string TargetResourceType => "Encounter";
        public abstract bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures);
        protected abstract bool IncludeEncounter(EncounterContext enc, IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> ipWindows);

        public HashSet<string> ComputeExcludedKeys(PatientCqlInput input)
        {
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var enc in input.Encounters)
            {
                if (!IncludeEncounter(enc, input.IpWindows))
                    excluded.Add($"Encounter/{enc.EncounterId}");
            }
            return excluded;
        }
    }

    /// <summary>
    /// ACH Monthly + Daily Encounter retrieval:
    /// IP encounters (qualifying class within measurement period) plus SDE Encounter
    /// (encounters NOT in IP whose period overlaps an IP encounter's period). Both
    /// reduce to: <c>encounter.period overlaps ANY IP.period</c> — an IP encounter
    /// trivially overlaps its own period, and SDE Encounter pulls in non-IP overlappers.
    ///
    /// Hypoglycemic only retrieves IP encounters (no SDE Encounter equivalent), so the
    /// same predicate applies — non-IP-overlapping encounters are excluded for both.
    /// </summary>
    private sealed class AchEncounterFilterProfile : EncounterFilterProfileBase
    {
        public override bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures) =>
            measures.Contains(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)
            || measures.Contains(ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation)
            || measures.Contains(ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation);

        protected override bool IncludeEncounter(EncounterContext enc, IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> ipWindows) =>
            ipWindows.AnyOverlaps(enc.PeriodStart, enc.PeriodEnd);
    }

    // ----- Specimen profiles -----

    private abstract class SpecimenFilterProfileBase : ICqlFilterProfile
    {
        public string TargetResourceType => "Specimen";
        public abstract bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures);
        protected abstract bool IncludeSpecimen(SpecimenContext s, PatientCqlInput input);

        public HashSet<string> ComputeExcludedKeys(PatientCqlInput input)
        {
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in input.Specimens)
            {
                if (!IncludeSpecimen(s, input))
                    excluded.Add($"Specimen/{s.ResourceId}");
            }
            return excluded;
        }

        protected static string ReferenceId(string? reference)
        {
            if (string.IsNullOrWhiteSpace(reference)) return string.Empty;
            var slash = reference.IndexOf('/');
            return slash >= 0 ? reference[(slash + 1)..] : reference;
        }

        protected static bool ReferencesPatient(string? reference, string patientId) =>
            !string.IsNullOrWhiteSpace(patientId)
            && string.Equals(ReferenceId(reference), patientId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ACH Monthly SDE Specimen: <c>Specimens.collection.collected overlaps IP.period</c>.
    /// </summary>
    private sealed class AchMonthlySpecimenFilterProfile : SpecimenFilterProfileBase
    {
        public override bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures) =>
            measures.Contains(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation);

        protected override bool IncludeSpecimen(SpecimenContext s, PatientCqlInput input) =>
            ReferencesPatient(s.SubjectReference, input.PatientId)
            && input.IpWindows.AnyOverlaps(s.CollectionStart, s.CollectionEnd);
    }

    /// <summary>
    /// ACH Daily returns only specimens reached through respiratory pathogen lab observations
    /// (COVID-19, influenza, or RSV value sets), not every generated or acquired Specimen.
    /// </summary>
    private sealed class AchDailySpecimenFilterProfile : SpecimenFilterProfileBase
    {
        private static readonly HashSet<string> RespiratoryPathogenObservationLoincCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "100343-3", "100344-1", "22825-4", "22827-0", "23769-3", "23781-8", "23782-6", "24015-0", "30075-6", "30076-4", "31858-4", "31859-2", "31860-0", "31863-4", "31864-2", "31949-1", "31950-9", "32040-8", "33045-6", "33535-6", "34487-9", "38270-5", "38271-3", "38272-1", "38381-0", "39025-2", "39102-9", "39103-7", "40982-1", "40987-0", "40988-8", "43874-7", "43895-2", "44263-2", "44264-0", "44265-7", "44266-5", "44558-5", "44559-3", "44560-1", "44561-9", "44562-7", "44563-5", "44564-3", "44566-8", "44567-6", "44571-8", "44572-6", "44573-4", "44574-2", "44575-9", "44576-7", "44577-5", "44795-3", "46082-4", "46083-2", "48509-4", "49521-8", "49524-2", "49528-3", "50329-2", "50700-4", "53250-7", "53251-5", "54240-7", "54243-1", "55463-4", "55464-2", "55465-9", "56024-3", "57985-4", "5860-2", "5861-0", "5862-8", "5863-6", "5864-4", "5865-1", "5866-9", "5867-7", "5874-3", "5875-0", "5876-8", "5877-6", "59423-4", "60538-6", "61101-2", "61102-0", "62462-7", "6435-2", "6436-0", "6437-8", "6438-6", "68966-1", "68986-9", "68987-7", "72356-9", "72365-0", "72366-8", "72367-6", "72885-7", "74038-1", "74039-9", "74040-7", "74784-0", "74785-7", "74786-5", "76077-7", "76078-5", "76079-3", "76080-1", "76088-4", "76089-2", "77022-2", "77023-0", "77026-3", "77027-1", "77028-9", "77383-8", "77384-6", "77389-5", "77390-3", "77605-4", "80382-5", "80383-3", "80588-7", "80589-5", "80590-3", "80591-1", "80597-8", "80598-6", "81305-5", "81307-1", "81308-9", "81309-7", "81320-4", "81321-2", "81325-3", "81327-9", "81428-5", "82166-0", "82167-8", "82168-6", "82169-4", "82170-2", "82176-9", "82461-5", "85477-8", "85478-6", "85479-4", "86317-5", "86565-9", "86568-3", "86569-1", "86571-7", "86572-5", "88187-0", "88193-8", "88194-6", "88195-3", "88202-7", "88204-3", "88528-5", "88592-1", "88595-4", "88596-2", "88597-0", "88599-6", "88600-2", "88835-4", "88904-8", "88905-5", "88909-7", "90886-3", "91072-9", "91133-9", "91771-6", "91794-8", "91795-5", "92131-2", "92141-1", "92142-9", "92808-5", "92809-3", "92957-0", "92976-0", "92977-8", "94307-6", "94308-4", "94309-2", "94310-0", "94311-8", "94312-6", "94313-4", "94314-2", "94315-9", "94316-7", "94394-4", "94395-1", "94396-9", "94500-6", "94502-2", "94509-7", "94510-5", "94511-3", "94532-9", "94533-7", "94534-5", "94558-4", "94559-2", "94565-9", "94639-2", "94640-0", "94641-8", "94642-6", "94643-4", "94644-2", "94645-9", "94646-7", "94647-5", "94660-8", "94745-7", "94746-5", "94756-4", "94757-2", "94758-0", "94759-8", "94760-6", "94765-5", "94766-3", "94767-1", "94819-0", "94822-4", "94845-5", "95209-3", "95406-5", "95409-9", "95424-8", "95425-5", "95521-1", "95522-9", "95608-6", "95609-4", "95658-1", "95823-1", "95824-9", "95970-0", "96091-4", "96119-3", "96120-1", "96121-9", "96122-7", "96123-5", "96448-6", "96756-2", "96757-0", "96763-8", "96764-6", "96765-3", "96797-6", "96898-2", "96899-0", "96900-6", "96957-6", "96958-4", "96986-5", "97097-0", "97098-8", "97104-4", "99623-1"
        };

        private static readonly HashSet<string> AllowedObservationStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "final", "registered", "preliminary", "partial"
        };

        public override bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures) =>
            measures.Contains(ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation);

        protected override bool IncludeSpecimen(SpecimenContext s, PatientCqlInput input)
        {
            if (input.IpWindows.Count == 0)
                return false;

            if (!ReferencesPatient(s.SubjectReference, input.PatientId))
                return false;

            return input.Observations.Any(o =>
                string.Equals(ReferenceId(o.SpecimenReference), s.ResourceId, StringComparison.OrdinalIgnoreCase)
                && o.HasCategory("laboratory")
                && AllowedObservationStatuses.Contains(o.Status)
                && RespiratoryPathogenObservationLoincCodes.Contains(o.LoincCode));
        }
    }

    /// <summary>
    /// Hypoglycemic SDE Specimen: <c>Specimens.collection.collected during IP.period</c>.
    /// </summary>
    private sealed class HypoglycemicSpecimenFilterProfile : SpecimenFilterProfileBase
    {
        public override bool AppliesToAny(IReadOnlyList<ProfiledMeasureType> measures) =>
            measures.Contains(ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation);

        protected override bool IncludeSpecimen(SpecimenContext s, PatientCqlInput input) =>
            ReferencesPatient(s.SubjectReference, input.PatientId)
            && input.IpWindows.AnyContains(s.CollectionStart, s.CollectionEnd);
    }
}
