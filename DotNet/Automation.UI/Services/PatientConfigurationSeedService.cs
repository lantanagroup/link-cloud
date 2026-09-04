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
    private static readonly Guid AchQualifyingAllStoriesId = new("00000000-0000-0000-3000-000000000003");
    private static readonly Guid PneumoniaNqId = new("00000000-0000-0000-3000-000000000004");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await store.DeleteAsync(AchQualifyingAllStoriesId, cancellationToken);

        await UpsertAsync(Build(
            PneumoniaId,
            "Pneumonia (inpatient)",
            "Inpatient pneumonia. Predicted qualifying for ACH from encounter class.",
            ClinicalScenarioIds.Pneumonia,
            inpatient: true,
            hypo: false), cancellationToken);

        await UpsertAsync(Build(
            DiabeticHypoId,
            "Diabetic hypoglycemia (inpatient + insulin)",
            "Inpatient diabetic hypoglycemia with the hypoglycemic insulin pair. Predicted qualifying for ACH and Hypo.",
            ClinicalScenarioIds.DiabeticHypoglycemia,
            inpatient: true,
            hypo: true), cancellationToken);

        await UpsertAsync(Build(
            PneumoniaNqId,
            "Pneumonia (ambulatory)",
            "Ambulatory pneumonia. Predicted non-qualifying for ACH because the encounter class is not an initial-population class.",
            ClinicalScenarioIds.Pneumonia,
            inpatient: false,
            hypo: false), cancellationToken);

        logger.LogInformation("Seeded system Patient Configurations.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static PatientConfiguration Build(
        Guid id,
        string name,
        string description,
        Guid scenarioId,
        bool inpatient,
        bool hypo)
    {
        var scenario = FhirGenerationCodes.GetScenarioById(scenarioId.ToString())!;
        var intent = PatientConfigurationTemplate.FromClinicalProfile(scenario, 50, inpatient, hypo);
        ConfigurationQualification.Stamp(
            intent,
            scenarioId.ToString(),
            out var eligibilities,
            out var cohortQualification);
        return new PatientConfiguration
        {
            Id = id,
            Name = name,
            Description = description,
            IsSystem = true,
            UpdatedAt = DateTimeOffset.UtcNow,
            CohortQualification = cohortQualification,
            MeasureEligibilities = eligibilities,
            ScheduledInpatientPattern = inpatient
                ? ScheduledStayWindow.DefaultPattern
                : ScheduledInpatientPattern.AdmittedDuringPeriodDischargedDuringPeriod,
            ClinicalScenarioIds = [scenarioId.ToString()],
            ResourcesPerPatientMin = 50,
            ResourcesPerPatientMax = 100,
            Intent = intent
        };
    }

    private async Task UpsertAsync(PatientConfiguration configuration, CancellationToken ct)
        => await store.UpsertAsync(configuration, ct);
}
