using Hl7.Fhir.Model;
using System.Globalization;
using System.Text.Json;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Extracts <see cref="CqlFilterSimulator.PatientCqlInput"/> from generated FHIR content —
/// either from in-memory <see cref="Bundle.EntryComponent"/> lists (pipeline path) or from
/// serialized transaction bundle JSON (batch/test path).
///
/// Both code paths feed the simulator identical data shapes (actual Condition attributes +
/// actual Encounter period), so prediction can never drift from the generator's internals.
/// </summary>
public static class CqlFilterInputExtractor
{
    // ---------- In-memory extraction (pipeline) ----------

    /// <summary>
    /// Builds a <see cref="CqlFilterSimulator.PatientCqlInput"/> from a patient's in-memory
    /// generated FHIR entries. Returns <c>null</c> if the patient has no encounter recorded.
    /// </summary>
    public static CqlFilterSimulator.PatientCqlInput? ExtractFromEntries(
        string patientId,
        IEnumerable<Bundle.EntryComponent> entries,
        IReadOnlyList<(string ResourceType, string ResourceId, string Key, JsonElement Resource)>? sharedResourceEntries = null)
    {
        var encounters = new List<Encounter>();
        var conditions = new List<Condition>();
        var observations = new List<Observation>();
        var diagnosticReports = new List<DiagnosticReport>();
        var procedures = new List<Procedure>();
        var medicationRequests = new List<MedicationRequest>();
        var medicationAdministrations = new List<MedicationAdministration>();
        var coverages = new List<Coverage>();
        var serviceRequests = new List<ServiceRequest>();
        var specimens = new List<Specimen>();
        var locations = new List<CqlFilterSimulator.LocationContext>();
        var medications = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            switch (entry.Resource)
            {
                case Encounter enc:
                    encounters.Add(enc);
                    break;
                case Condition cond:
                    conditions.Add(cond);
                    break;
                case Observation obs:
                    observations.Add(obs);
                    break;
                case DiagnosticReport diagnosticReport:
                    diagnosticReports.Add(diagnosticReport);
                    break;
                case Procedure proc:
                    procedures.Add(proc);
                    break;
                case MedicationRequest mr:
                    medicationRequests.Add(mr);
                    break;
                case MedicationAdministration ma:
                    medicationAdministrations.Add(ma);
                    break;
                case Coverage cov:
                    coverages.Add(cov);
                    break;
                case ServiceRequest sr:
                    serviceRequests.Add(sr);
                    break;
                case Specimen specimen:
                    specimens.Add(specimen);
                    break;
                case Location location:
                    locations.Add(new CqlFilterSimulator.LocationContext(location.Id ?? string.Empty));
                    break;
                case Medication medication:
                    if (!string.IsNullOrWhiteSpace(medication.Id))
                        medications[medication.Id] = ExtractCodeableConceptCodes(medication.Code);
                    break;
            }
        }

        foreach (var (id, codes) in ExtractMedicationCodes(sharedResourceEntries))
        {
            if (!medications.ContainsKey(id))
                medications[id] = codes;
        }

        foreach (var loc in ExtractLocations(sharedResourceEntries))
        {
            if (locations.Any(existing =>
                    string.Equals(existing.ResourceId, loc.ResourceId, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            locations.Add(loc);
        }

        if (encounters.Count == 0)
            return null;

        // Use the first encounter with a populated Period as the legacy "primary" anchor
        // for the EncounterId/Start/End triple. The full encounter list (in the order they
        // were extracted) is what the IP resolver actually consumes; the legacy fields are
        // kept for back-compat with pre-Landing-3 tests and consumers.
        var primaryEncounter = encounters.FirstOrDefault(e => e.Period != null) ?? encounters[0];
        var primaryStart = ParseFhirDateTime(primaryEncounter.Period?.Start) ?? DateTime.MinValue;
        var primaryEnd = ParseFhirDateTime(primaryEncounter.Period?.End) ?? DateTime.MaxValue;

        var encounterContexts = encounters.Select(BuildEncounterContext).ToList();
        var conditionContexts = conditions.Select(BuildConditionContext).ToList();
        var observationContexts = observations.Select(BuildObservationContext).ToList();
        var diagnosticReportContexts = diagnosticReports.Select(BuildDiagnosticReportContext).ToList();
        var procedureContexts = procedures.Select(BuildProcedureContext).ToList();
        var medicationRequestContexts = medicationRequests.Select(mr => BuildMedicationRequestContext(mr, medications)).ToList();
        var medicationAdministrationContexts = medicationAdministrations.Select(ma => BuildMedicationAdministrationContext(ma, medications)).ToList();
        var coverageContexts = coverages.Select(BuildCoverageContext).ToList();
        var serviceRequestContexts = serviceRequests.Select(BuildServiceRequestContext).ToList();
        var specimenContexts = specimens.Select(BuildSpecimenContext).ToList();

        return new CqlFilterSimulator.PatientCqlInput(
            patientId,
            primaryEncounter.Id,
            primaryStart,
            primaryEnd,
            conditionContexts,
            observationContexts)
        {
            Encounters = encounterContexts,
            DiagnosticReports = diagnosticReportContexts,
            Procedures = procedureContexts,
            MedicationRequests = medicationRequestContexts,
            MedicationAdministrations = medicationAdministrationContexts,
            Coverages = coverageContexts,
            ServiceRequests = serviceRequestContexts,
            Specimens = specimenContexts,
            Locations = locations
        };
    }

    internal static List<CqlFilterSimulator.LocationContext> ExtractLocations(
        IReadOnlyList<(string ResourceType, string ResourceId, string Key, JsonElement Resource)>? entries)
    {
        var locations = new List<CqlFilterSimulator.LocationContext>();
        if (entries == null)
            return locations;

        foreach (var (resourceType, resourceId, _, _) in entries)
        {
            if (!string.Equals(resourceType, "Location", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(resourceId))
            {
                continue;
            }

            locations.Add(new CqlFilterSimulator.LocationContext(resourceId));
        }

        return locations;
    }

    private static CqlFilterSimulator.EncounterContext BuildEncounterContext(Encounter enc)
    {
        var start = ParseFhirDateTime(enc.Period?.Start) ?? DateTime.MinValue;
        var end = ParseFhirDateTime(enc.Period?.End) ?? DateTime.MaxValue;
        var classCode = enc.Class?.Code ?? string.Empty;
        var status = enc.Status?.ToString() ?? string.Empty;
        var locationRefs = (enc.Location ?? [])
            .Select(loc => loc.Location?.Reference)
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Select(reference => reference!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var diagnosisIds = (enc.Diagnosis ?? [])
            .Select(diagnosis => diagnosis.Condition?.Reference)
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Select(reference => reference!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new CqlFilterSimulator.EncounterContext(enc.Id ?? string.Empty, start, end, classCode, status)
        {
            LocationReferences = locationRefs,
            DiagnosisConditionIds = diagnosisIds
        };
    }

    private static CqlFilterSimulator.ConditionContext BuildConditionContext(Condition cond)
    {
        var isActive = cond.ClinicalStatus?.Coding?
            .Any(c => string.Equals(c.Code, "active", StringComparison.OrdinalIgnoreCase)) ?? false;

        var categories = (cond.Category ?? [])
            .SelectMany(cat => cat.Coding ?? [])
            .Select(c => c.Code)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var recordedDate = ParseFhirDateTime(cond.RecordedDate) ?? DateTime.MinValue;
        var encounterReference = cond.Encounter?.Reference ?? string.Empty;
        DateTime onsetStart;
        DateTime onsetEnd;
        switch (cond.Onset)
        {
            case Period p:
                onsetStart = ParseFhirDateTime(p.Start) ?? recordedDate;
                onsetEnd = ParseFhirDateTime(p.End) ?? onsetStart;
                break;
            case FhirDateTime dt:
                onsetStart = ParseFhirDateTime(dt.Value) ?? recordedDate;
                onsetEnd = onsetStart;
                break;
            default:
                onsetStart = recordedDate;
                onsetEnd = recordedDate;
                break;
        }

        return new CqlFilterSimulator.ConditionContext(
            cond.Id,
            isActive,
            recordedDate.Date,
            encounterReference,
            categories)
        {
            OnsetStart = onsetStart,
            OnsetEnd = onsetEnd
        };
    }

    private static DateTime? ParseFhirDateTime(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            return parsed;
        return null;
    }

    private static CqlFilterSimulator.ObservationContext BuildObservationContext(Observation obs)
    {
        var loinc = (obs.Code?.Coding ?? [])
            .FirstOrDefault(c => string.Equals(c.System, "http://loinc.org", StringComparison.OrdinalIgnoreCase))?.Code
            ?? string.Empty;

        var categories = (obs.Category ?? [])
            .SelectMany(cat => cat.Coding ?? [])
            .Select(c => c.Code)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        DateTime effectiveStart;
        DateTime effectiveEnd;
        switch (obs.Effective)
        {
            case Period p:
                effectiveStart = ParseFhirDateTime(p.Start) ?? DateTime.MinValue;
                effectiveEnd = ParseFhirDateTime(p.End) ?? effectiveStart;
                break;
            case FhirDateTime dt:
                effectiveStart = ParseFhirDateTime(dt.Value) ?? DateTime.MinValue;
                effectiveEnd = effectiveStart;
                break;
            default:
                effectiveStart = DateTime.MinValue;
                effectiveEnd = DateTime.MaxValue;
                break;
        }

        return new CqlFilterSimulator.ObservationContext(
            obs.Id,
            loinc,
            categories,
            effectiveStart,
            effectiveEnd)
        {
            Status = obs.Status?.ToString() ?? string.Empty,
            SpecimenReference = obs.Specimen?.Reference ?? string.Empty
        };
    }

    private static CqlFilterSimulator.DiagnosticReportContext BuildDiagnosticReportContext(DiagnosticReport report)
    {
        DateTime effectiveStart;
        DateTime effectiveEnd;

        switch (report.Effective)
        {
            case Period p:
                effectiveStart = ParseFhirDateTime(p.Start) ?? DateTime.MinValue;
                effectiveEnd = ParseFhirDateTime(p.End) ?? effectiveStart;
                break;
            case FhirDateTime dt:
                effectiveStart = ParseFhirDateTime(dt.Value) ?? DateTime.MinValue;
                effectiveEnd = effectiveStart;
                break;
            default:
                effectiveStart = DateTime.MinValue;
                effectiveEnd = DateTime.MaxValue;
                break;
        }

        var categories = (report.Category ?? [])
            .SelectMany(cat => cat.Coding ?? [])
            .Select(c => c.Code)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var resultRefs = (report.Result ?? [])
            .Select(r => r.Reference)
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Select(reference => reference!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CqlFilterSimulator.DiagnosticReportContext(
            report.Id,
            effectiveStart,
            effectiveEnd)
        {
            CategoryCodes = categories,
            Codes = ExtractCodeableConceptCodes(report.Code),
            ResultReferences = resultRefs,
            Status = report.Status?.ToString() ?? string.Empty
        };
    }

    // ---------- Procedure / MedicationRequest / MedicationAdministration / Coverage / ServiceRequest ----------

    private static CqlFilterSimulator.ProcedureContext BuildProcedureContext(Procedure proc)
    {
        DateTime performedStart;
        DateTime performedEnd;
        switch (proc.Performed)
        {
            case Period p:
                performedStart = ParseFhirDateTime(p.Start) ?? DateTime.MinValue;
                performedEnd = ParseFhirDateTime(p.End) ?? performedStart;
                break;
            case FhirDateTime dt:
                performedStart = ParseFhirDateTime(dt.Value) ?? DateTime.MinValue;
                performedEnd = performedStart;
                break;
            default:
                performedStart = DateTime.MinValue;
                performedEnd = DateTime.MaxValue;
                break;
        }

        return new CqlFilterSimulator.ProcedureContext(
            proc.Id,
            performedStart,
            performedEnd,
            proc.Encounter?.Reference ?? string.Empty)
        {
            Codes = ExtractCodeableConceptCodes(proc.Code)
        };
    }

    private static CqlFilterSimulator.MedicationRequestContext BuildMedicationRequestContext(
        MedicationRequest mr,
        IReadOnlyDictionary<string, List<string>> medicationCodes)
    {
        var authoredOn = ParseFhirDateTime(mr.AuthoredOn) ?? DateTime.MinValue;
        return new CqlFilterSimulator.MedicationRequestContext(
            mr.Id,
            authoredOn,
            mr.Encounter?.Reference ?? string.Empty)
        {
            MedicationCodes = ResolveMedicationCodes(mr.Medication, medicationCodes),
            Intent = mr.Intent?.ToString() ?? string.Empty
        };
    }

    private static CqlFilterSimulator.MedicationAdministrationContext BuildMedicationAdministrationContext(
        MedicationAdministration ma,
        IReadOnlyDictionary<string, List<string>> medicationCodes)
    {
        DateTime effectiveStart;
        DateTime effectiveEnd;
        switch (ma.Effective)
        {
            case Period p:
                effectiveStart = ParseFhirDateTime(p.Start) ?? DateTime.MinValue;
                effectiveEnd = ParseFhirDateTime(p.End) ?? effectiveStart;
                break;
            case FhirDateTime dt:
                effectiveStart = ParseFhirDateTime(dt.Value) ?? DateTime.MinValue;
                effectiveEnd = effectiveStart;
                break;
            default:
                effectiveStart = DateTime.MinValue;
                effectiveEnd = DateTime.MaxValue;
                break;
        }

        return new CqlFilterSimulator.MedicationAdministrationContext(
            ma.Id,
            effectiveStart,
            effectiveEnd,
            ma.Context?.Reference ?? string.Empty)
        {
            MedicationCodes = ResolveMedicationCodes(ma.Medication, medicationCodes),
            Status = ma.Status?.ToString() ?? string.Empty
        };
    }

    private static CqlFilterSimulator.CoverageContext BuildCoverageContext(Coverage cov)
    {
        // Coverage.period is optional; missing start defaults to MinValue, missing end to
        // MaxValue (open-ended coverage). This matches FHIR overlap semantics where an
        // unbounded end means the coverage is still active.
        var periodStart = ParseFhirDateTime(cov.Period?.Start) ?? DateTime.MinValue;
        var periodEnd = ParseFhirDateTime(cov.Period?.End) ?? DateTime.MaxValue;
        return new CqlFilterSimulator.CoverageContext(cov.Id, periodStart, periodEnd);
    }

    private static CqlFilterSimulator.ServiceRequestContext BuildServiceRequestContext(ServiceRequest sr)
    {
        var authoredOn = ParseFhirDateTime(sr.AuthoredOn) ?? DateTime.MinValue;
        return new CqlFilterSimulator.ServiceRequestContext(
            sr.Id,
            authoredOn,
            sr.Encounter?.Reference ?? string.Empty)
        {
            Codes = ExtractCodeableConceptCodes(sr.Code),
            Intent = sr.Intent?.ToString() ?? string.Empty
        };
    }

    private static CqlFilterSimulator.SpecimenContext BuildSpecimenContext(Specimen specimen)
    {
        DateTime collectionStart;
        DateTime collectionEnd;
        switch (specimen.Collection?.Collected)
        {
            case Period p:
                collectionStart = ParseFhirDateTime(p.Start) ?? DateTime.MinValue;
                collectionEnd = ParseFhirDateTime(p.End) ?? collectionStart;
                break;
            case FhirDateTime dt:
                collectionStart = ParseFhirDateTime(dt.Value) ?? DateTime.MinValue;
                collectionEnd = collectionStart;
                break;
            default:
                collectionStart = DateTime.MinValue;
                collectionEnd = DateTime.MaxValue;
                break;
        }

        return new CqlFilterSimulator.SpecimenContext(
            specimen.Id,
            collectionStart,
            collectionEnd)
        {
            SubjectReference = specimen.Subject?.Reference ?? string.Empty
        };
    }

    private static List<string> ExtractCodeableConceptCodes(CodeableConcept? concept)
    {
        if (concept?.Coding == null)
            return [];
        return concept.Coding
            .Select(c => c.Code)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ResolveMedicationCodes(
        DataType? medication,
        IReadOnlyDictionary<string, List<string>> medicationCodes)
    {
        var fromConcept = ExtractCodeableConceptCodes(medication as CodeableConcept);
        if (fromConcept.Count > 0)
            return fromConcept;

        var reference = (medication as ResourceReference)?.Reference;
        if (string.IsNullOrWhiteSpace(reference))
            return [];

        var slash = reference.IndexOf('/');
        var id = slash >= 0 ? reference[(slash + 1)..] : reference;
        return medicationCodes.TryGetValue(id, out var codes) ? codes : [];
    }

    internal static Dictionary<string, List<string>> ExtractMedicationCodes(
        IReadOnlyList<(string ResourceType, string ResourceId, string Key, JsonElement Resource)>? entries)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (entries == null)
            return result;

        foreach (var (resourceType, resourceId, _, resource) in entries)
        {
            if (!string.Equals(resourceType, "Medication", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(resourceId))
            {
                continue;
            }

            result[resourceId] = ExtractCodesFromJson(resource);
        }

        return result;
    }

    private static List<string> ExtractCodesFromJson(JsonElement resource)
    {
        var codes = new List<string>();
        if (!resource.TryGetProperty("code", out var code) || code.ValueKind != JsonValueKind.Object)
            return codes;
        if (!code.TryGetProperty("coding", out var coding) || coding.ValueKind != JsonValueKind.Array)
            return codes;

        foreach (var item in coding.EnumerateArray())
        {
            if (!item.TryGetProperty("code", out var codeProp) || codeProp.ValueKind != JsonValueKind.String)
                continue;
            var value = codeProp.GetString();
            if (!string.IsNullOrWhiteSpace(value)
                && !codes.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                codes.Add(value);
            }
        }

        return codes;
    }
}
