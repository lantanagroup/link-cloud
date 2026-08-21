namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Classic factory path behind <see cref="IPatientEntryGenerator"/>.
/// MockFhirServer keeps this as the default (A5).
/// </summary>
public sealed class ClassicPatientEntryGenerator : IPatientEntryGenerator
{
    public static ClassicPatientEntryGenerator Shared { get; } = new();

    public Task<List<Hl7.Fhir.Model.Bundle.EntryComponent>> GenerateAsync(
        PatientEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var patientId = string.IsNullOrWhiteSpace(request.PatientId)
            ? request.Ids.PatientId(request.PatientIndex)
            : request.PatientId.Trim();
        var seed = request.BaseSeed + (request.Profile.SeedOffset ?? request.PatientIndex);

        var entries = FhirBundleGenerator.GeneratePatientEntries(
            patientId,
            request.Ids,
            request.SharedPractitionerIds,
            request.SharedMedicationIds,
            request.TotalResourcesPerPatient,
            seed,
            request.Config,
            request.RequirementsPlan,
            request.ClinicalPeriodStart,
            request.ClinicalPeriodEnd);

        return Task.FromResult(entries);
    }
}
