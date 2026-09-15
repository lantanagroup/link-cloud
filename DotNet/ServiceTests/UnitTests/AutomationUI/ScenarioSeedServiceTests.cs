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

    [Fact]
    public async global::System.Threading.Tasks.Task Adhoc_report_daily_ach_scenario_uses_daily_measure_and_distinct_org_id()
    {
        var store = new InMemoryScenarioStore();
        var sut = new ScenarioSeedService(store, NullLogger<ScenarioSeedService>.Instance);

        await sut.StartAsync(CancellationToken.None);

        var monthly = await store.GetByIdAsync(AdhocReportScenarioId, CancellationToken.None);
        monthly.Should().NotBeNull();
        monthly!.IsSystemScenario.Should().BeTrue();
        monthly.SelectedMeasures.Should().ContainSingle()
            .Which.Should().Be(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation);
        monthly.NhsnOrganizationId.Should().Be("10756");

        var daily = await store.GetByIdAsync(AdhocReportDailyAchScenarioId, CancellationToken.None);
        daily.Should().NotBeNull();
        daily!.IsSystemScenario.Should().BeTrue();
        daily.Name.Should().Be("Adhoc Report Daily ACH Test");
        daily.ReportMethod.Should().Be(ReportMethod.Adhoc);
        daily.SelectedMeasures.Should().ContainSingle()
            .Which.Should().Be(ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation);
        daily.NhsnOrganizationId.Should().Be("10764");
        daily.NhsnOrganizationId.Should().NotBe(monthly.NhsnOrganizationId);
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
