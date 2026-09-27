using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using LantanaGroup.Automation.Generation;

namespace Automation.UI.Services;

internal static class SystemPatientConfigurationIds
{
    public static readonly Guid PneumoniaInpatient = new("00000000-0000-0000-3000-000000000001");
    public static readonly Guid DiabeticHypoglycemia = new("00000000-0000-0000-3000-000000000002");
    public static readonly Guid PneumoniaAmbulatory = new("00000000-0000-0000-3000-000000000004");
}

public sealed class PatientConfigurationSeedService(
    IPatientConfigurationStore store,
    ILogger<PatientConfigurationSeedService> logger) : IHostedService
{
    private static readonly Guid PneumoniaId = SystemPatientConfigurationIds.PneumoniaInpatient;
    private static readonly Guid DiabeticHypoId = SystemPatientConfigurationIds.DiabeticHypoglycemia;
    private static readonly Guid AchQualifyingAllStoriesId = new("00000000-0000-0000-3000-000000000003");
    private static readonly Guid PneumoniaNqId = SystemPatientConfigurationIds.PneumoniaAmbulatory;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await store.DeleteAsync(AchQualifyingAllStoriesId, cancellationToken);

        await UpsertAsync(Build(
            PneumoniaId,
            "Pneumonia (inpatient)",
            "Inpatient pneumonia.",
            ClinicalScenarioIds.Pneumonia,
            inpatient: true,
            hypo: false), cancellationToken);

        await UpsertAsync(Build(
            DiabeticHypoId,
            "Diabetic hypoglycemia (inpatient + insulin)",
            "Inpatient diabetic hypoglycemia with insulin.",
            ClinicalScenarioIds.DiabeticHypoglycemia,
            inpatient: true,
            hypo: true), cancellationToken);

        await UpsertAsync(Build(
            PneumoniaNqId,
            "Pneumonia (ambulatory)",
            "Ambulatory pneumonia.",
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
