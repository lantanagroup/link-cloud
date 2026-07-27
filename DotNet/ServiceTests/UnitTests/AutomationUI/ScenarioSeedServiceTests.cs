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
    private static readonly Guid MultiMeasureScenarioId = new("00000000-0000-0000-0000-000000000006");

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
