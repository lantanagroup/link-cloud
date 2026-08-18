using Hl7.Fhir.Model;
using LantanaGroup.Automation.Helpers;
using System.Globalization;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Loads imported-patient FHIR data ahead of the generation pipeline so that
/// (a) data is fetched / parsed exactly once per run, and
/// (b) the run's clinical period can be expanded to cover the actual encounter
///     dates carried by the imported data — without this, an imported patient
///     whose encounter falls outside the (otherwise-default) report period is
///     silently classified non-qualifying by measure-eval even when the
///     <see cref="ImportedPatientClassifier"/> reported them as qualifying.
/// </summary>
public static class ImportedPatientLoader
{
    /// <summary>
    /// Materializes <see cref="ImportedPatientInput.PreLoadedEntries"/> for every
    /// imported patient. ID-based imports are fetched via
    /// <c>Patient/{id}/$everything</c>; bundle imports are parsed from their stored
    /// JSON. Throws on the first failure so the run aborts (per scenario contract).
    /// </summary>
    public static async Task LoadAllAsync(
        FhirDataLoader fhirDataLoader,
        IEnumerable<ImportedPatientInput> imports,
        IAutomationOutput? output = null,
        CancellationToken ct = default)
    {
        if (imports == null) return;

        foreach (var imp in imports)
        {
            ct.ThrowIfCancellationRequested();
            if (imp == null) continue;
            if (imp.PreLoadedEntries is { Count: > 0 }) continue; // idempotent

            string bundleJson;
            if (imp.Source == ImportedPatientSource.ExistingId)
            {
                if (string.IsNullOrWhiteSpace(imp.PatientId))
                    throw new InvalidOperationException("Imported patient (by ID) is missing PatientId.");
                output?.WriteLine($"  [imported:id] Pre-fetching Patient/{imp.PatientId}/$everything...");
                bundleJson = await fhirDataLoader.FetchPatientEverythingAsync(imp.PatientId.Trim(), ct);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(imp.BundleJson))
                    throw new InvalidOperationException(
                        $"Imported bundle for patient '{imp.PatientId}' has no bundle JSON.");
                bundleJson = imp.BundleJson!;
            }

            imp.PreLoadedEntries = ParseBundleEntries(bundleJson, imp.PatientId);

            // Backfill PatientId from the bundle when the user didn't specify one.
            if (string.IsNullOrWhiteSpace(imp.PatientId))
            {
                var p = imp.PreLoadedEntries
                    .Select(e => e?.Resource)
                    .OfType<Patient>()
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(p?.Id))
                    imp.PatientId = p!.Id;
            }
        }
    }

    /// <summary>
    /// Computes the union of encounter periods across the supplied imported patients,
    /// returning <c>(min start, max end)</c> as UTC <see cref="DateTime"/>s. Returns
    /// <c>(null, null)</c> when no encounter dates are present (e.g. before pre-load
    /// or when imports carry no Encounter resources).
    /// </summary>
    public static (DateTime? MinStart, DateTime? MaxEnd) ComputeEncounterDateRange(
        IEnumerable<ImportedPatientInput> imports)
    {
        DateTime? minStart = null;
        DateTime? maxEnd = null;

        foreach (var imp in imports ?? [])
        {
            if (imp?.PreLoadedEntries == null) continue;
            foreach (var entry in imp.PreLoadedEntries)
            {
                if (entry?.Resource is not Encounter enc || enc.Period == null) continue;

                var s = ParseFhirDateTime(enc.Period.Start);
                var e = ParseFhirDateTime(enc.Period.End);

                if (s.HasValue && (minStart == null || s.Value < minStart.Value)) minStart = s;
                if (e.HasValue && (maxEnd == null || e.Value > maxEnd.Value)) maxEnd = e;
                // Single-instant encounters: use start as end too.
                if (s.HasValue && !e.HasValue && (maxEnd == null || s.Value > maxEnd.Value)) maxEnd = s;
            }
        }

        return (minStart, maxEnd);
    }

    /// <summary>
    /// Parses a FHIR Bundle JSON string into a list of <see cref="Bundle.EntryComponent"/>
    /// items with <c>Request.Url</c> populated as <c>Type/id</c>. Accepts both transaction
    /// bundles (which already carry request entries) and searchset bundles (e.g. the output
    /// of <c>Patient/{id}/$everything</c>). Throws when the bundle is unparseable, has no
    /// entries, or is missing a Patient resource matching <paramref name="expectedPatientId"/>.
    /// </summary>
    public static List<Bundle.EntryComponent> ParseBundleEntries(string bundleJson, string? expectedPatientId)
    {
        Bundle? bundle;
        try
        {
            bundle = JsonSerializer.Deserialize<Bundle>(
                bundleJson,
                FhirSerializerOptions.ForFhirWithoutValidation());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse FHIR bundle JSON: {ex.Message}", ex);
        }

        if (bundle?.Entry == null || bundle.Entry.Count == 0)
            throw new InvalidOperationException("FHIR bundle contained no entries.");

        var foundPatient = false;
        var result = new List<Bundle.EntryComponent>(bundle.Entry.Count);
        foreach (var entry in bundle.Entry)
        {
            if (entry?.Resource == null) continue;

            var typeName = entry.Resource.TypeName;
            var id = entry.Resource.Id;
            if (string.IsNullOrWhiteSpace(typeName) || string.IsNullOrWhiteSpace(id))
                continue;

            entry.Request ??= new Bundle.RequestComponent();
            if (string.IsNullOrWhiteSpace(entry.Request.Url))
            {
                entry.Request.Url = $"{typeName}/{id}";
                entry.Request.Method = Bundle.HTTPVerb.PUT;
            }

            if (string.Equals(typeName, "Patient", StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(expectedPatientId)
                    || string.Equals(id, expectedPatientId, StringComparison.Ordinal)))
            {
                foundPatient = true;
            }

            result.Add(entry);
        }

        if (!string.IsNullOrWhiteSpace(expectedPatientId) && !foundPatient)
            throw new InvalidOperationException(
                $"FHIR bundle does not contain Patient/{expectedPatientId}. Bundle imports must include a Patient resource whose id matches the configured value.");

        return result;
    }

    private static DateTime? ParseFhirDateTime(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            return parsed.UtcDateTime;
        return null;
    }
}
