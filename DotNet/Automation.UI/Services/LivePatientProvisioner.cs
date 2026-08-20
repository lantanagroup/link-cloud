using Hl7.Fhir.Model;
using LantanaGroup.Automation;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Automation.Helpers;
using Task = System.Threading.Tasks.Task;

namespace Automation.UI.Services;

/// <summary>
/// Mid-window Generate / Upload / Reference-ID provisioner. Appends generation or
/// import tracking onto the run's existing <see cref="GenerationManifest"/>.
/// Census Admit/Discharge never goes through this type.
/// </summary>
internal sealed class LivePatientProvisioner(
    Guid runId,
    IAutomationOutput output,
    FhirDataLoader fhirDataLoader,
    GenerationManifest manifest,
    IReadOnlyList<ProfiledMeasureType> selectedMeasures,
    int resourcesPerPatient,
    int? generationSeed,
    FhirGenerationConfig? generationConfig,
    GenerationRequirementsPlan? generationRequirementsPlan,
    FhirGenerationPipeline.AcquisitionSimulationConfig? acquisitionSimulation,
    ISnapshotStore snapshotStore) : ILivePatientProvisioner
{
    public async Task<LiveProvisionedPatient> GenerateQualifyingPatientAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var eligibilities = selectedMeasures.ToDictionary(m => m, _ => MeasureEligibility.Qualifying);
        var profile = new PatientProfile(
            eligibilities,
            SeedOffset: manifest.PatientIds.Count + 10_000,
            ScheduledInpatientPattern: ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod);

        var (patientId, effectiveProfile) = await FhirGenerationPipeline.GenerateAndAppendPatientAsync(
            output,
            fhirDataLoader,
            manifest,
            profile,
            selectedMeasures,
            resourcesPerPatient,
            generationSeed,
            generationConfig,
            generationRequirementsPlan,
            acquisitionSimulation);

        await PersistManifestAsync(cancellationToken);
        return ToProvisioned(patientId, effectiveProfile);
    }

    public async Task<LiveProvisionedPatient> UploadPatientAsync(
        string content,
        string? fileName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var imported = BuildBundleImport(content, fileName);
        var (patientId, effectiveProfile) = await FhirGenerationPipeline.ImportAndAppendPatientAsync(
            output,
            fhirDataLoader,
            manifest,
            imported,
            selectedMeasures,
            acquisitionSimulation);

        await PersistManifestAsync(cancellationToken);
        return ToProvisioned(patientId, effectiveProfile);
    }

    public async Task<LiveProvisionedPatient> ReferencePatientAsync(
        string patientId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var imported = new ImportedPatientInput
        {
            Source = ImportedPatientSource.ExistingId,
            PatientId = patientId.Trim(),
            AutoDetect = true
        };

        var (id, effectiveProfile) = await FhirGenerationPipeline.ImportAndAppendPatientAsync(
            output,
            fhirDataLoader,
            manifest,
            imported,
            selectedMeasures,
            acquisitionSimulation);

        await PersistManifestAsync(cancellationToken);
        return ToProvisioned(id, effectiveProfile);
    }

    private LiveProvisionedPatient ToProvisioned(string patientId, PatientProfile profile)
        => new(patientId, profile.IsExpectedToBeSubmitted(selectedMeasures));

    private async Task PersistManifestAsync(CancellationToken cancellationToken)
    {
        await snapshotStore.SetDomainAsync(runId, "generationManifest", manifest.ToSnapshot(), cancellationToken);
    }

    private static ImportedPatientInput BuildBundleImport(string content, string? fileName)
    {
        var imported = new ImportedPatientInput
        {
            Source = ImportedPatientSource.Bundle,
            BundleJson = content,
            FileName = fileName,
            AutoDetect = true
        };

        try
        {
            var entries = ImportedPatientLoader.ParseBundleEntries(content, imported.PatientId);
            imported.PreLoadedEntries = entries;
            var patient = entries.Select(e => e.Resource).OfType<Patient>().FirstOrDefault();
            imported.PatientId = !string.IsNullOrWhiteSpace(patient?.Id)
                ? patient!.Id
                : $"live-upload-{Guid.NewGuid():N}";
            return imported;
        }
        catch (InvalidOperationException)
        {
            var id = ExtractPatientId(content) ?? $"live-upload-{Guid.NewGuid():N}";
            imported.PatientId = id;
            imported.BundleJson = WrapAsTransactionBundle(content, id);
            return imported;
        }
    }

    private static string? ExtractPatientId(string content)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("id", out var id)
                && id.ValueKind == System.Text.Json.JsonValueKind.String
                && !string.IsNullOrWhiteSpace(id.GetString()))
            {
                return id.GetString();
            }
        }
        catch (System.Text.Json.JsonException)
        {
        }

        return null;
    }

    private static string WrapAsTransactionBundle(string resourceJson, string patientId)
        => "{\"resourceType\":\"Bundle\",\"type\":\"transaction\",\"entry\":[{\"resource\":"
           + resourceJson
           + ",\"request\":{\"method\":\"PUT\",\"url\":\"Patient/" + patientId + "\"}}]}";
}
