using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using LantanaGroup.Automation.Generation;

namespace Automation.UI.Services;

public sealed class PatientConfigurationSeedService(
    IPatientConfigurationStore store,
    ILogger<PatientConfigurationSeedService> logger) : IHostedService
{
    private static readonly Guid PneumoniaId = new("00000000-0000-0000-3000-000000000001");
    private static readonly Guid DiabeticHypoId = new("00000000-0000-0000-3000-000000000002");
    private static readonly Guid AchQualifyingId = new("00000000-0000-0000-3000-000000000003");
    private static readonly Guid AchNonQualifyingId = new("00000000-0000-0000-3000-000000000004");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var ach = ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation;
        var hypo = ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation;

        await UpsertAsync(new PatientConfiguration
        {
            Id = PneumoniaId,
            Name = "Pneumonia (ACH qualifying)",
            Description = "Inpatient pneumonia story pack. Quick setup equivalent.",
            IsSystem = true,
            UpdatedAt = DateTimeOffset.UtcNow,
            CohortQualification = MeasureEligibility.Qualifying,
            MeasureEligibilities = new() { [ach] = MeasureEligibility.Qualifying },
            ClinicalScenarioIds = [ClinicalScenarioIds.Pneumonia.ToString()],
            ResourcesPerPatientMin = 50,
            ResourcesPerPatientMax = 100
        }, cancellationToken);

        await UpsertAsync(new PatientConfiguration
        {
            Id = DiabeticHypoId,
            Name = "Diabetic hypoglycemia (ACH + Hypo)",
            Description = "Inpatient diabetic hypoglycemia with insulin pair.",
            IsSystem = true,
            UpdatedAt = DateTimeOffset.UtcNow,
            CohortQualification = MeasureEligibility.Qualifying,
            MeasureEligibilities = new()
            {
                [ach] = MeasureEligibility.Qualifying,
                [hypo] = MeasureEligibility.Qualifying
            },
            ClinicalScenarioIds = [ClinicalScenarioIds.DiabeticHypoglycemia.ToString()],
            ResourcesPerPatientMin = 50,
            ResourcesPerPatientMax = 100,
            Intent = new PatientGenerationIntent { IncludeHypoglycemicInsulin = true }
        }, cancellationToken);

        await UpsertAsync(new PatientConfiguration
        {
            Id = AchQualifyingId,
            Name = "ACH qualifying (all stories)",
            Description = "Qualifying ACH patients rotating through all clinical story packs.",
            IsSystem = true,
            UpdatedAt = DateTimeOffset.UtcNow,
            CohortQualification = MeasureEligibility.Qualifying,
            MeasureEligibilities = new() { [ach] = MeasureEligibility.Qualifying },
            ClinicalScenarioIds = [],
            ResourcesPerPatientMin = 50,
            ResourcesPerPatientMax = 100
        }, cancellationToken);

        await UpsertAsync(new PatientConfiguration
        {
            Id = AchNonQualifyingId,
            Name = "ACH non-qualifying (all stories)",
            Description = "Non-qualifying ACH cohort. Story packs remain selectable independently of qualification.",
            IsSystem = true,
            UpdatedAt = DateTimeOffset.UtcNow,
            CohortQualification = MeasureEligibility.NonQualifying,
            MeasureEligibilities = new() { [ach] = MeasureEligibility.NonQualifying },
            ClinicalScenarioIds = [],
            ResourcesPerPatientMin = 50,
            ResourcesPerPatientMax = 100
        }, cancellationToken);

        logger.LogInformation("Seeded system Patient Configurations.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task UpsertAsync(PatientConfiguration configuration, CancellationToken ct)
    {
        await store.UpsertAsync(configuration, ct);
    }
}
