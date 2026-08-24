using Hl7.Fhir.Model;
using LantanaGroup.Automation.Helpers;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Per-patient FHIR synthesis. Default implementation is Thetis Engine
/// (<see cref="Thetis.ThetisPatientEntryGenerator"/>).
/// </summary>
public interface IPatientEntryGenerator
{
    Task<List<Bundle.EntryComponent>> GenerateAsync(PatientEntryRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Shared Org/Loc/Pract/Med/facility-Device upload. Built by factories until a shared-infra graph exists.
/// </summary>
public interface ISharedInfrastructureGenerator
{
    (FhirBundleGenerator.SharedIds Ids,
     List<Bundle.EntryComponent> SharedEntries,
     List<string> PractitionerIds,
     List<string> MedicationIds)
        Generate(GenerationRequirementsPlan? plan, string runTag);
}

public sealed class PatientEntryRequest
{
    public required PatientProfile Profile { get; init; }
    public required int PatientIndex { get; init; }
    public required int BaseSeed { get; init; }
    public required int TotalResourcesPerPatient { get; init; }
    public required List<string> SharedPractitionerIds { get; init; }
    public required List<string> SharedMedicationIds { get; init; }
    public required IReadOnlyList<ProfiledMeasureType> Measures { get; init; }
    public DateTime? ClinicalPeriodStart { get; init; }
    public DateTime? ClinicalPeriodEnd { get; init; }
    public FhirGenerationConfig? Config { get; init; }
    public GenerationRequirementsPlan? RequirementsPlan { get; init; }
    public required FhirBundleGenerator.SharedIds Ids { get; init; }

    /// <summary>
    /// When set (MockFhirServer, imported IDs), used instead of
    /// <c>Ids.PatientId(PatientIndex)</c>.
    /// </summary>
    public string? PatientId { get; init; }

    public IAutomationOutput? Output { get; init; }
}

/// <summary>
/// Factory-backed Org/Loc/Pract/Med/facility-Device. Used by the Thetis
/// pipeline until a shared-infra graph exists.
/// </summary>
public sealed class FactorySharedInfrastructureGenerator : ISharedInfrastructureGenerator
{
    public static FactorySharedInfrastructureGenerator Shared { get; } = new();

    public (FhirBundleGenerator.SharedIds Ids,
            List<Bundle.EntryComponent> SharedEntries,
            List<string> PractitionerIds,
            List<string> MedicationIds)
        Generate(GenerationRequirementsPlan? plan, string runTag)
    {
        var ids = new FhirBundleGenerator.SharedIds(runTag);
        var (entries, practitionerIds, medicationIds) =
            ScenarioResourceGeneration.BuildSharedInfrastructure(ids, plan);
        return (ids, entries, practitionerIds, medicationIds);
    }
}
