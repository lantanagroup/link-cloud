using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using LantanaGroup.Automation.Generation;

namespace Automation.UI.Services;

/// <summary>
/// Ensures preloaded system scenarios exist in the database on application startup.
/// These scenarios mirror the backend E2E tests and are read-only (cannot be modified or deleted,
/// but can be cloned).
/// </summary>
public sealed class ScenarioSeedService : IHostedService
{
    private readonly IScenarioStore _store;
    private readonly ILogger<ScenarioSeedService> _logger;

    // Deterministic IDs so seeding is idempotent.
    private static readonly Guid AdhocReportTestId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid MultiPatientId = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid MegaPatientId = new("00000000-0000-0000-0000-000000000003");
    private static readonly Guid ScheduledReportId = new("00000000-0000-0000-0000-000000000004");
    private static readonly Guid RegenerateReportId = new("00000000-0000-0000-0000-000000000005");
    private static readonly Guid MultiMeasureId = new("00000000-0000-0000-0000-000000000006");
    private static readonly Guid MegaMultiPatientId = new("00000000-0000-0000-0000-000000000007");
    private static readonly Guid ApiHealthScenarioId = new("00000000-0000-0000-0000-000000000008");

    private static readonly List<ProfiledMeasureType> DefaultMeasures =
        [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation];

    private static readonly List<string> DefaultEligibleScenarioIds =
        [.. ClinicalScenarioEligibility.GetEligibleScenarioIds(DefaultMeasures, MeasureEligibility.Qualifying)];

    private static readonly List<string> DefaultNonQualifyingScenarioIds =
        [.. ClinicalScenarioEligibility.GetEligibleScenarioIds(DefaultMeasures, MeasureEligibility.NonQualifying)];

    private static readonly Dictionary<ProfiledMeasureType, MeasureEligibility> DefaultQualifyingEligibilities =
        DefaultMeasures.ToDictionary(m => m, _ => MeasureEligibility.Qualifying);

    private static readonly Dictionary<ProfiledMeasureType, MeasureEligibility> DefaultNonQualifyingEligibilities =
        DefaultMeasures.ToDictionary(m => m, _ => MeasureEligibility.NonQualifying);

    public ScenarioSeedService(IScenarioStore store, ILogger<ScenarioSeedService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var systemScenarios = BuildSystemScenarios();

        foreach (var scenario in systemScenarios)
        {
            var existing = await _store.GetByIdAsync(scenario.Id, cancellationToken);
            if (existing == null)
            {
                await _store.UpsertAsync(scenario, cancellationToken);
                _logger.LogInformation("Seeded system scenario: {Name} ({Id})", scenario.Name, scenario.Id);
            }
            else
            {
                // Always overwrite system scenarios to keep them in sync with code.
                scenario.UpdatedAt = DateTimeOffset.UtcNow;
                await _store.UpsertAsync(scenario, cancellationToken);
                _logger.LogDebug("Refreshed system scenario: {Name} ({Id})", scenario.Name, scenario.Id);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static List<TestScenarioDefinition> BuildSystemScenarios() =>
    [
        // --- Adhoc Report Test (ad-hoc, 1 patient, 1000 resources) ---
        new TestScenarioDefinition
        {
            Id = AdhocReportTestId,
            Name = "Adhoc Report Test",
            Description = "Single patient ad-hoc report. Mirrors the AdhocReportTest backend E2E test.",
            IsSystemScenario = true,
            ReportMethod = ReportMethod.Adhoc,
            SelectedMeasures = [..DefaultMeasures],
            Seed = 20260326,
            PatientCount = 1,
            ResourcesPerPatientMin = 1000,
            ResourcesPerPatientMax = 1000,
            PatientCohorts =
            [
                new PatientCohortDefinition
                {
                    PatientCount = 1,
                    MeasureEligibilities = new(DefaultQualifyingEligibilities),
                    EligibleClinicalScenarioIds = [..DefaultEligibleScenarioIds],
                    ResourcesPerPatientMin = 1000,
                    ResourcesPerPatientMax = 1000
                }
            ],
            CleanupServiceData = false,
            CleanupFhirData = true,
        },

        // --- API Health Scenario (scheduled seed workflow used by API Health tests) ---
        new TestScenarioDefinition
        {
            Id = ApiHealthScenarioId,
            Name = "ApiHealthScenario",
            Description = "System scenario for API Health stateful seeding and diagnostics.",
            IsSystemScenario = true,
            ReportMethod = ReportMethod.Adhoc,
            SelectedMeasures = [..DefaultMeasures],
            Seed = 20260501,
            PatientCount = 1,
            ResourcesPerPatientMin = 15,
            ResourcesPerPatientMax = 15,
            PatientCohorts =
            [
                new PatientCohortDefinition
                {
                    PatientCount = 1,
                    MeasureEligibilities = new(DefaultQualifyingEligibilities),
                    EligibleClinicalScenarioIds = [..DefaultEligibleScenarioIds],
                    ResourcesPerPatientMin = 15,
                    ResourcesPerPatientMax = 15
                }
            ],
            CleanupServiceData = false,
            CleanupFhirData = true,
        },

        // --- Multi Patient Test (ad-hoc, 150 patients, 25-50 resources each) ---
        new TestScenarioDefinition
        {
            Id = MultiPatientId,
            Name = "Multi Patient Test",
            Description = "Volume test with 150 patients, 25–50 resources each. Mirrors the MultiPatientTest backend E2E test.",
            IsSystemScenario = true,
            ReportMethod = ReportMethod.Adhoc,
            SelectedMeasures = [..DefaultMeasures],
            Seed = 20260328,
            PatientCount = 150,
            ResourcesPerPatientMin = 25,
            ResourcesPerPatientMax = 50,
            PatientCohorts =
            [
                new PatientCohortDefinition
                {
                    PatientCount = 150,
                    MeasureEligibilities = new(DefaultQualifyingEligibilities),
                    EligibleClinicalScenarioIds = [..DefaultEligibleScenarioIds],
                    ResourcesPerPatientMin = 25,
                    ResourcesPerPatientMax = 50
                }
            ],
            CleanupServiceData = false,
            CleanupFhirData = true,
        },

        // --- Mega Patient Test (ad-hoc, 1 patient, ~5000 resources) ---
        new TestScenarioDefinition
        {
            Id = MegaPatientId,
            Name = "Mega Patient Test",
            Description = "Stress test with a single patient and ~5,000 resources. Mirrors the MegaPatientTest backend E2E test.",
            IsSystemScenario = true,
            ReportMethod = ReportMethod.Adhoc,
            SelectedMeasures = [..DefaultMeasures],
            Seed = 20260327,
            PatientCount = FhirBundleGenerator.DefaultPatientCount,
            ResourcesPerPatientMin = FhirBundleGenerator.DefaultResourcesPerPatient,
            ResourcesPerPatientMax = FhirBundleGenerator.DefaultResourcesPerPatient,
            PatientCohorts =
            [
                new PatientCohortDefinition
                {
                    PatientCount = FhirBundleGenerator.DefaultPatientCount,
                    MeasureEligibilities = new(DefaultQualifyingEligibilities),
                    EligibleClinicalScenarioIds = [..DefaultEligibleScenarioIds],
                    ResourcesPerPatientMin = FhirBundleGenerator.DefaultResourcesPerPatient,
                    ResourcesPerPatientMax = FhirBundleGenerator.DefaultResourcesPerPatient
                }
            ],
            CleanupServiceData = false,
            CleanupFhirData = true,
        },

        // --- Mega Multi Patient Test (ad-hoc, 150 patients, 5000 resources for first, 25-50 for the rest) ---
        new TestScenarioDefinition
        {
            Id = MegaMultiPatientId,
            Name = "Mega Multi Patient Test",
            Description = "Hybrid stress + volume test: one mega patient with ~5,000 resources plus 149 patients with 25–50 resources each.",
            IsSystemScenario = true,
            ReportMethod = ReportMethod.Adhoc,
            SelectedMeasures = [..DefaultMeasures],
            Seed = 20260330,
            PatientCount = 150,
            ResourcesPerPatientMin = 25,
            ResourcesPerPatientMax = 5000,
            PatientCohorts =
            [
                new PatientCohortDefinition
                {
                    PatientCount = 1,
                    MeasureEligibilities = new(DefaultQualifyingEligibilities),
                    EligibleClinicalScenarioIds = [..DefaultEligibleScenarioIds],
                    ResourcesPerPatientMin = 5000,
                    ResourcesPerPatientMax = 5000
                },
                new PatientCohortDefinition
                {
                    PatientCount = 149,
                    MeasureEligibilities = new(DefaultQualifyingEligibilities),
                    EligibleClinicalScenarioIds = [..DefaultEligibleScenarioIds],
                    ResourcesPerPatientMin = 25,
                    ResourcesPerPatientMax = 50
                }
            ],
            CleanupServiceData = false,
            CleanupFhirData = true,
        },

        // --- Scheduled Report Test (scheduled, 6 patients with explicit inpatient timing patterns) ---
        new TestScenarioDefinition
        {
            Id = ScheduledReportId,
            Name = "Scheduled Report Test",
            Description = "Exercises the full scheduled report workflow with multiple inpatient timing patterns (before/during/after report period admit/discharge combinations). Mirrors the ReportScheduledWorkflowTest.",
            IsSystemScenario = true,
            ReportMethod = ReportMethod.ScheduledReport,
            SelectedMeasures = [..DefaultMeasures],
            Seed = 20260326,
            PatientCount = 6,
            ResourcesPerPatientMin = 50,
            ResourcesPerPatientMax = 100,
            PatientCohorts =
            [
                new PatientCohortDefinition
                {
                    PatientCount = 1,
                    MeasureEligibilities = new(DefaultQualifyingEligibilities),
                    EligibleClinicalScenarioIds = [..DefaultEligibleScenarioIds],
                    ResourcesPerPatientMin = 50,
                    ResourcesPerPatientMax = 100,
                    ScheduledInpatientPattern = ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod
                },
                new PatientCohortDefinition
                {
                    PatientCount = 1,
                    MeasureEligibilities = new(DefaultQualifyingEligibilities),
                    EligibleClinicalScenarioIds = [..DefaultEligibleScenarioIds],
                    ResourcesPerPatientMin = 50,
                    ResourcesPerPatientMax = 100,
                    ScheduledInpatientPattern = ScheduledInpatientPattern.AdmittedBeforePeriodDischargedDuringPeriod
                },
                new PatientCohortDefinition
                {
                    PatientCount = 1,
                    MeasureEligibilities = new(DefaultQualifyingEligibilities),
                    EligibleClinicalScenarioIds = [..DefaultEligibleScenarioIds],
                    ResourcesPerPatientMin = 50,
                    ResourcesPerPatientMax = 100,
                    ScheduledInpatientPattern = ScheduledInpatientPattern.AdmittedDuringPeriodRemainsInpatientAfterPeriod
                },
                new PatientCohortDefinition
                {
                    PatientCount = 1,
                    MeasureEligibilities = new(DefaultQualifyingEligibilities),
                    EligibleClinicalScenarioIds = [..DefaultEligibleScenarioIds],
                    ResourcesPerPatientMin = 50,
                    ResourcesPerPatientMax = 100,
                    ScheduledInpatientPattern = ScheduledInpatientPattern.AdmittedDuringPeriodDischargedDuringPeriod
                },
                new PatientCohortDefinition
                {
                    PatientCount = 1,
                    CohortQualification = MeasureEligibility.NonQualifying,
                    MeasureEligibilities = new(DefaultNonQualifyingEligibilities),
                    EligibleClinicalScenarioIds = [..DefaultNonQualifyingScenarioIds],
                    ResourcesPerPatientMin = 50,
                    ResourcesPerPatientMax = 100,
                    ScheduledInpatientPattern = ScheduledInpatientPattern.AdmittedAndDischargedBeforePeriod
                },
                new PatientCohortDefinition
                {
                    PatientCount = 1,
                    CohortQualification = MeasureEligibility.NonQualifying,
                    MeasureEligibilities = new(DefaultNonQualifyingEligibilities),
                    EligibleClinicalScenarioIds = [..DefaultNonQualifyingScenarioIds],
                    ResourcesPerPatientMin = 50,
                    ResourcesPerPatientMax = 100,
                    ScheduledInpatientPattern = ScheduledInpatientPattern.AdmittedAndDischargedAfterPeriod
                }
            ],
            CleanupServiceData = false,
            CleanupFhirData = true,
        },

        // --- Regenerate Report Test
        new TestScenarioDefinition
        {
            Id = RegenerateReportId,
            Name = "Regenerate Report Test",
            Description = "Produces a scheduled report, then regenerates it. Mirrors the RegenerateReportTest.",
            IsSystemScenario = true,
            ReportMethod = ReportMethod.RegenerateReport,
            SelectedMeasures = [..DefaultMeasures],
            Seed = 20260401,
            PatientCount = 1,
            ResourcesPerPatientMin = 100,
            ResourcesPerPatientMax = 100,
            PatientCohorts =
            [
                new PatientCohortDefinition
                {
                    PatientCount = 1,
                    MeasureEligibilities = new(DefaultQualifyingEligibilities),
                    EligibleClinicalScenarioIds = [..DefaultEligibleScenarioIds],
                    ResourcesPerPatientMin = 100,
                    ResourcesPerPatientMax = 100
                }
            ],
            CleanupServiceData = false,
            CleanupFhirData = true,
        },

        // --- Multi Measure Test (ad-hoc, 2 patients, ACH + Hypo, 250 resources each) ---
        new TestScenarioDefinition
        {
            Id = MultiMeasureId,
            Name = "Multi Measure Test",
            Description = "Multi-measure ad-hoc test with ACH + Hypoglycemic. Patient 1 qualifies for both, patient 2 qualifies ACH only. Mirrors the MultiMeasureTest backend E2E test.",
            IsSystemScenario = true,
            ReportMethod = ReportMethod.Adhoc,
            SelectedMeasures =
            [
                ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
                ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation
            ],
            Seed = 20260420,
            PatientCount = 2,
            ResourcesPerPatientMin = 250,
            ResourcesPerPatientMax = 250,
            PatientCohorts =
            [
                // Cohort 1: qualifies for both ACH and Hypo (inpatient + diabetic med)
                new PatientCohortDefinition
                {
                    PatientCount = 1,
                    MeasureEligibilities = new Dictionary<ProfiledMeasureType, MeasureEligibility>
                    {
                        [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation] = MeasureEligibility.Qualifying,
                        [ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation] = MeasureEligibility.Qualifying
                    },
                    EligibleClinicalScenarioIds =
                    [
                        ..ClinicalScenarioEligibility.GetEligibleScenarioIds(
                        [
                            ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
                            ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation
                        ], MeasureEligibility.Qualifying)
                    ],
                    ResourcesPerPatientMin = 250,
                    ResourcesPerPatientMax = 250,
                    ScheduledInpatientPattern = ScheduledInpatientPattern.AdmittedDuringPeriodDischargedDuringPeriod
                },
                // Cohort 2: qualifies for ACH only (inpatient, no Hypo med)
                new PatientCohortDefinition
                {
                    PatientCount = 1,
                    MeasureEligibilities = new Dictionary<ProfiledMeasureType, MeasureEligibility>
                    {
                        [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation] = MeasureEligibility.Qualifying,
                        [ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation] = MeasureEligibility.NonQualifying
                    },
                    EligibleClinicalScenarioIds = [..DefaultEligibleScenarioIds],
                    ResourcesPerPatientMin = 250,
                    ResourcesPerPatientMax = 250,
                    ScheduledInpatientPattern = ScheduledInpatientPattern.AdmittedDuringPeriodDischargedDuringPeriod
                }
            ],
            CleanupServiceData = false,
            CleanupFhirData = true,
        },
    ];
}
