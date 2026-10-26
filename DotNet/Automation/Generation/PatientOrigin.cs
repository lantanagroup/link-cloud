namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Whether a manifest patient was produced by the FHIR generator or supplied
/// as an uploaded (imported) patient.
/// </summary>
public static class PatientOrigin
{
    public const string Generated = "Generated";
    public const string Uploaded = "Uploaded";

    /// <summary>
    /// Classifies each patient id. A template-cache key means the FHIR generator
    /// produced the patient. Every other manifest row is an uploaded patient
    /// (existing FHIR id or supplied bundle).
    /// </summary>
    public static Dictionary<string, string> Resolve(
        IReadOnlyList<string> patientIds,
        IEnumerable<string>? preExistingPatientIds,
        IReadOnlyDictionary<string, string>? templateCacheKeyByPatient)
    {
        var preExisting = new HashSet<string>(StringComparer.Ordinal);
        if (preExistingPatientIds != null)
        {
            foreach (var id in preExistingPatientIds)
            {
                if (!string.IsNullOrWhiteSpace(id))
                    preExisting.Add(id);
            }
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var patientId in patientIds)
        {
            if (string.IsNullOrWhiteSpace(patientId) || result.ContainsKey(patientId))
                continue;

            if (preExisting.Contains(patientId))
            {
                result[patientId] = Uploaded;
                continue;
            }

            if (templateCacheKeyByPatient != null
                && templateCacheKeyByPatient.TryGetValue(patientId, out var templateKey)
                && !string.IsNullOrWhiteSpace(templateKey))
            {
                result[patientId] = Generated;
                continue;
            }

            result[patientId] = Uploaded;
        }

        return result;
    }
}
