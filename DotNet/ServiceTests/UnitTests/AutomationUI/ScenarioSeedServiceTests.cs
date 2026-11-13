using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using LantanaGroup.Automation.Generation;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class ScenarioSeedServiceTests
{
    private static readonly Guid AdhocReportScenarioId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid AdhocReportDailyAchScenarioId = new("00000000-0000-0000-0000-000000000009");
    private static readonly Guid MultiMeasureScenarioId = new("00000000-0000-0000-0000-000000000006");
    private static readonly Guid DmrpScheduledReportScenarioId = new("00000000-0000-0000-0000-000000000010");

    [Fact]
    public async global::System.Threading.Tasks.Task Adhoc_report_daily_ach_scenario_uses_the_daily_measure()
    {
        var store = new InMemoryScenarioStore();
        var sut = new ScenarioSeedService(store, NullLogger<ScenarioSeedService>.Instance);

        await sut.StartAsync(CancellationToken.None);

        var monthly = await store.GetByIdAsync(AdhocReportScenarioId, CancellationToken.None);
        monthly.Should().NotBeNull();
        monthly!.IsSystemScenario.Should().BeTrue();
        monthly.SelectedMeasures.Should().ContainSingle()
            .Which.Should().Be(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation);
        monthly.FacilityConfigurationMode.Should().Be(FacilityConfigurationMode.Facility);
        monthly.FacilityTemplateId.Should().Be(FacilityTemplateCatalog.SystemDefaultId);

        var daily = await store.GetByIdAsync(AdhocReportDailyAchScenarioId, CancellationToken.None);
        daily.Should().NotBeNull();
        daily!.IsSystemScenario.Should().BeTrue();
        daily.Name.Should().Be("Adhoc Report Daily ACH Test");
        daily.ReportMethod.Should().Be(ReportMethod.Adhoc);
        daily.SelectedMeasures.Should().ContainSingle()
            .Which.Should().Be(ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation);
        daily.PatientCount.Should().Be(1);
        daily.ResourcesPerPatientMin.Should().Be(1000);
        daily.ResourcesPerPatientMax.Should().Be(1000);
        daily.ReportPeriodStart.Should().Be(new DateTimeOffset(2023, 1, 15, 0, 0, 0, TimeSpan.Zero));
        daily.ReportPeriodEnd.Should().Be(new DateTimeOffset(2023, 1, 15, 23, 59, 59, TimeSpan.Zero));
        daily.PatientCohorts.Should().ContainSingle();
        daily.PatientCohorts[0].MeasureEligibilities.Should().ContainKey(
            ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation);
        daily.PatientCohorts[0].ScheduledInpatientPattern.Should().Be(
            ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Multi_measure_seeded_scenario_sets_inpatient_pattern_for_each_cohort()
    {
        var store = new InMemoryScenarioStore();
        var sut = new ScenarioSeedService(store, NullLogger<ScenarioSeedService>.Instance);

        await sut.StartAsync(CancellationToken.None);

        var scenario = await store.GetByIdAsync(MultiMeasureScenarioId, CancellationToken.None);
        scenario.Should().NotBeNull();
        scenario!.PatientCohorts.Should().HaveCount(2);
        scenario.PatientCohorts.Should().OnlyContain(c =>
            c.ScheduledInpatientPattern == ScheduledInpatientPattern.AdmittedDuringPeriodDischargedDuringPeriod);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Dmrp_scheduled_report_scenario_is_seeded()
    {
        var store = new InMemoryScenarioStore();
        var sut = new ScenarioSeedService(
            store,
            NullLogger<ScenarioSeedService>.Instance);

        await sut.StartAsync(CancellationToken.None);

        var scenario = await store.GetByIdAsync(
            DmrpScheduledReportScenarioId,
            CancellationToken.None);

        scenario.Should().NotBeNull();
        scenario!.IsSystemScenario.Should().BeTrue();

        scenario.Name.Should().Be("DMRP Scheduled Report Test");
        scenario.ReportMethod.Should().Be(ReportMethod.ScheduledReport);

        scenario.EnableDmrp.Should().BeTrue();

        scenario.SelectedMeasures.Should().ContainSingle()
            .Which.Should().Be(
                ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation);

        scenario.PatientCount.Should().Be(1);

        scenario.PatientCohorts.Should().ContainSingle();
        scenario.PatientCohorts[0].PatientCount.Should().Be(1);
        scenario.PatientCohorts[0].MeasureEligibilities.Should().ContainKey(
            ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation);

        scenario.PatientCohorts[0].ScheduledInpatientPattern.Should().Be(
            ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Startup_refreshes_system_scenarios_and_leaves_user_scenarios_unchanged()
    {
        var store = new InMemoryScenarioStore();
        var user = new TestScenarioDefinition
        {
            Id = Guid.NewGuid(),
            Name = "User scenario",
            Description = "keep me",
            IsSystemScenario = false,
            EnableDmrp = true,
            PatientCount = 4
        };
        await store.UpsertAsync(user, CancellationToken.None);

        var sut = new ScenarioSeedService(store, NullLogger<ScenarioSeedService>.Instance);
        await sut.StartAsync(CancellationToken.None);

        var saved = await store.GetByIdAsync(user.Id, CancellationToken.None);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("User scenario");
        saved.Description.Should().Be("keep me");
        saved.EnableDmrp.Should().BeTrue();
        saved.PatientCount.Should().Be(4);
        saved.IsSystemScenario.Should().BeFalse();

        var monthly = await store.GetByIdAsync(AdhocReportScenarioId, CancellationToken.None);
        monthly.Should().NotBeNull();
        monthly!.IsSystemScenario.Should().BeTrue();
        monthly.EnableDmrp.Should().BeFalse();
    }

    private sealed class InMemoryScenarioStore : IScenarioStore
    {
        private readonly Dictionary<Guid, TestScenarioDefinition> _items = new();

        public global::System.Threading.Tasks.Task<List<TestScenarioDefinition>> GetAllAsync(CancellationToken ct = default)
            => global::System.Threading.Tasks.Task.FromResult(_items.Values.ToList());

        public global::System.Threading.Tasks.Task<TestScenarioDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            _items.TryGetValue(id, out var scenario);
            return global::System.Threading.Tasks.Task.FromResult<TestScenarioDefinition?>(scenario);
        }

        public global::System.Threading.Tasks.Task UpsertAsync(TestScenarioDefinition scenario, CancellationToken ct = default)
        {
            _items[scenario.Id] = scenario;
            return global::System.Threading.Tasks.Task.CompletedTask;
        }

        public global::System.Threading.Tasks.Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            _items.Remove(id);
            return global::System.Threading.Tasks.Task.CompletedTask;
        }
    }
}
