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
        IEnumerable<Bundle.EntryComponent> entries)
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
            }
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
        var medicationRequestContexts = medicationRequests.Select(BuildMedicationRequestContext).ToList();
        var medicationAdministrationContexts = medicationAdministrations.Select(BuildMedicationAdministrationContext).ToList();
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
            Specimens = specimenContexts
        };
    }

    private static CqlFilterSimulator.EncounterContext BuildEncounterContext(Encounter enc)
    {
        var start = ParseFhirDateTime(enc.Period?.Start) ?? DateTime.MinValue;
        var end = ParseFhirDateTime(enc.Period?.End) ?? DateTime.MaxValue;
        var classCode = enc.Class?.Code ?? string.Empty;
        var status = enc.Status?.ToString() ?? string.Empty;
        return new CqlFilterSimulator.EncounterContext(enc.Id ?? string.Empty, start, end, classCode, status);
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

        return new CqlFilterSimulator.ConditionContext(
            cond.Id,
            isActive,
            recordedDate.Date,
            encounterReference,
            categories);
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

        return new CqlFilterSimulator.DiagnosticReportContext(
            report.Id,
            effectiveStart,
            effectiveEnd);
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
            proc.Encounter?.Reference ?? string.Empty);
    }

    private static CqlFilterSimulator.MedicationRequestContext BuildMedicationRequestContext(MedicationRequest mr)
    {
        var authoredOn = ParseFhirDateTime(mr.AuthoredOn) ?? DateTime.MinValue;
        return new CqlFilterSimulator.MedicationRequestContext(
            mr.Id,
            authoredOn,
            mr.Encounter?.Reference ?? string.Empty);
    }

    private static CqlFilterSimulator.MedicationAdministrationContext BuildMedicationAdministrationContext(MedicationAdministration ma)
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
            ma.Context?.Reference ?? string.Empty);
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
            sr.Encounter?.Reference ?? string.Empty);
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
}
