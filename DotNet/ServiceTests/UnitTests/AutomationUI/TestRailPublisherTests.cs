using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Automation.UI.Models;
using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.ApiHealth;
using Automation.UI.Services.ApiHealth.Seeding;
using Automation.UI.Services.ApiHealth.TestSuites;
using Automation.UI.Services.Persistence;
using Automation.UI.Services.TestRail;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class TestRailStatusMapperTests
{
    [Theory]
    [InlineData(AutomationRunStatus.Succeeded, TestRailStatusMapper.Passed)]
    [InlineData(AutomationRunStatus.Failed, TestRailStatusMapper.Failed)]
    [InlineData(AutomationRunStatus.Cancelled, TestRailStatusMapper.Blocked)]
    [InlineData(AutomationRunStatus.Running, TestRailStatusMapper.Failed)]
    public void FromScenarioStatus_maps_known_outcomes(AutomationRunStatus status, int expected)
        => TestRailStatusMapper.FromScenarioStatus(status).Should().Be(expected);

    [Fact]
    public void FromApiHealthResult_maps_pass_and_fail()
    {
        TestRailStatusMapper.FromApiHealthResult(passed: true, skipped: false)
            .Should().Be(TestRailStatusMapper.Passed);
        TestRailStatusMapper.FromApiHealthResult(passed: false, skipped: false)
            .Should().Be(TestRailStatusMapper.Failed);
    }

    [Fact]
    public void FromApiHealthResult_omits_skipped()
    {
        TestRailStatusMapper.FromApiHealthResult(passed: false, skipped: true).Should().BeNull();
        TestRailStatusMapper.FromApiHealthResult(passed: true, skipped: true).Should().BeNull();
    }
}

[Trait("Category", "UnitTests")]
public class TestRailCaseMapperTests
{
    [Fact]
    public void Scenario_property_wins_over_mapping_config()
    {
        var map = new Dictionary<string, int> { ["alpha"] = 99 };
        TestRailCaseMapper.ResolveScenarioCaseId(42, Guid.NewGuid(), "alpha", map)
            .Should().Be(42);
    }

    [Fact]
    public void Scenario_map_matches_id_then_name()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var map = new Dictionary<string, int>
        {
            [id.ToString()] = 11,
            ["Smoke"] = 22
        };

        TestRailCaseMapper.ResolveScenarioCaseId(null, id, "other", map).Should().Be(11);
        TestRailCaseMapper.ResolveScenarioCaseId(null, Guid.NewGuid(), "Smoke", map).Should().Be(22);
    }

    [Fact]
    public void Missing_or_zero_mapping_returns_null()
    {
        TestRailCaseMapper.ResolveScenarioCaseId(null, Guid.NewGuid(), "none", new Dictionary<string, int>())
            .Should().BeNull();
        TestRailCaseMapper.ResolveScenarioCaseId(0, null, "none", new Dictionary<string, int> { ["none"] = 0 })
            .Should().BeNull();
    }

    [Fact]
    public void ApiHealth_property_wins_over_endpoint_key_map()
    {
        var map = new Dictionary<string, int> { ["Tenant::Create"] = 7 };
        TestRailCaseMapper.ResolveApiHealthCaseId(15, "Tenant::Create", "Create", map)
            .Should().Be(15);
        TestRailCaseMapper.ResolveApiHealthCaseId(null, "Tenant::Create", "Create", map)
            .Should().Be(7);
    }
}

[Trait("Category", "UnitTests")]
public class TestRailPublisherTests
{
    [Fact]
    public async Task Disabled_or_incomplete_config_is_noop()
    {
        var api = new Mock<ITestRailApiClient>(MockBehavior.Strict);
        var publisher = CreatePublisher(api.Object, new TestRailOptions { Enabled = false });

        await publisher.PublishScenarioRunAsync(SampleScenarioRequest());
        await publisher.PublishApiHealthRunAsync(SampleApiHealthRequest());

        api.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Missing_case_id_is_noop()
    {
        var api = new Mock<ITestRailApiClient>(MockBehavior.Strict);
        var logs = new CaptureLogger<TestRailPublisher>();
        var publisher = CreatePublisher(api.Object, ConfiguredOptions(), logger: logs);

        await publisher.PublishScenarioRunAsync(SampleScenarioRequest());

        api.VerifyNoOtherCalls();
        logs.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Warning
            && entry.Message.Contains("no TestRail case id mapped", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Scenario_success_creates_run_and_posts_passed_result()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.AddRunAsync(9, 100, It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(501);
        api.Setup(a => a.AddResultsForCasesAsync(501, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TestRailResultDto { Id = 1, CaseId = 42 }]);

        var scenarioId = Guid.NewGuid();
        var scenarios = new Mock<IScenarioStore>();
        scenarios.Setup(s => s.GetByIdAsync(scenarioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestScenarioDefinition { Id = scenarioId, Name = "Smoke", TestRailCaseId = 42 });

        var publisher = CreatePublisher(api.Object, ConfiguredOptions(), scenarios.Object);
        await publisher.PublishScenarioRunAsync(SampleScenarioRequest() with
        {
            ScenarioId = scenarioId,
            Status = AutomationRunStatus.Succeeded
        });

        api.Verify(a => a.AddRunAsync(
            9,
            100,
            It.Is<string>(n => n.Contains("Smoke", StringComparison.Ordinal)),
            It.Is<IReadOnlyList<int>>(ids => ids.Single() == 42),
            It.IsAny<CancellationToken>()), Times.Once);

        api.Verify(a => a.AddResultsForCasesAsync(
            501,
            It.Is<IReadOnlyList<TestRailCaseResult>>(r =>
                r.Count == 1
                && r[0].CaseId == 42
                && r[0].StatusId == TestRailStatusMapper.Passed
                && r[0].Attachment == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Scenario_failure_attaches_logs()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(8);
        api.Setup(a => a.AddResultsForCasesAsync(8, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TestRailResultDto { Id = 77, CaseId = 42 }]);
        api.Setup(a => a.AddAttachmentToResultAsync(77, It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var scenarioId = Guid.NewGuid();
        var publisher = CreatePublisher(api.Object, ConfiguredOptions(), ScenarioStore(scenarioId));

        await publisher.PublishScenarioRunAsync(SampleScenarioRequest() with
        {
            ScenarioId = scenarioId,
            Status = AutomationRunStatus.Failed,
            Error = "boom",
            Logs = ["line 1", "line 2"]
        });

        api.Verify(a => a.AddAttachmentToResultAsync(
            77,
            It.Is<string>(n => n.EndsWith(".log", StringComparison.Ordinal)),
            It.Is<byte[]>(b => Encoding.UTF8.GetString(b).Contains("boom")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Non_shared_scenario_run_keeps_hyphenated_automation_run_id()
    {
        var runId = Guid.Parse("8fd02ea1-3d9b-448c-926a-9ff274456c73");
        var hyphenated = runId.ToString("D");
        var compact = runId.ToString("N");

        string? runName = null;
        IReadOnlyList<TestRailCaseResult>? posted = null;
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.AddRunAsync(9, 100, It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .Callback<int, int, string, IReadOnlyList<int>, CancellationToken>((_, _, name, _, _) => runName = name)
            .ReturnsAsync(501);
        api.Setup(a => a.AddResultsForCasesAsync(501, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .Callback<int, IReadOnlyList<TestRailCaseResult>, CancellationToken>((_, results, _) => posted = results)
            .ReturnsAsync([new TestRailResultDto { Id = 77, CaseId = 42 }]);

        var scenarioId = Guid.NewGuid();
        var publisher = CreatePublisher(api.Object, ConfiguredOptions(), ScenarioStore(scenarioId));
        await publisher.PublishScenarioRunAsync(SampleScenarioRequest() with
        {
            RunId = runId,
            ScenarioId = scenarioId,
            RunName = "Smoke",
            Status = AutomationRunStatus.Failed,
            Error = "boom",
            Logs = ["line 1"]
        });

        runName.Should().Be($"Link Automation UI - Smoke - {hyphenated}");
        runName.Should().NotContain(compact);
        var result = posted.Should().ContainSingle().Subject;
        result.Comment.Should().Contain($"Automation.UI scenario run {hyphenated}");
        result.Comment.Should().NotContain(compact);
        result.AttachmentFileName.Should().Be($"scenario-run-{hyphenated}.log");
    }

    [Fact]
    public async Task Non_shared_api_health_run_keeps_hyphenated_automation_run_id()
    {
        var runId = Guid.Parse("8b8c9fab-72c2-4a5b-b200-271587e5d5d3");
        var hyphenated = runId.ToString("D");
        var compact = runId.ToString("N");

        string? runName = null;
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.AddRunAsync(9, 200, It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .Callback<int, int, string, IReadOnlyList<int>, CancellationToken>((_, _, name, _, _) => runName = name)
            .ReturnsAsync(44);
        api.Setup(a => a.AddResultsForCasesAsync(44, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var definition = new ApiEndpointDefinition
        {
            ServiceName = "Tenant",
            EndpointName = "Create Facility",
            TestRailCaseId = 1
        };
        var store = new Mock<IApiHealthRunStore>();
        store.Setup(s => s.GetLatestResultsForRunAsync(runId, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, ApiTestRunResult>
            {
                [definition.Key] = new()
                {
                    EndpointKey = definition.Key,
                    EndpointName = definition.EndpointName,
                    ServiceName = "Tenant",
                    Passed = true
                }
            });

        var publisher = CreatePublisher(
            api.Object,
            ConfiguredOptions(),
            apiHealthStore: store.Object,
            registry: new ApiEndpointRegistry([new FakeSuite(definition)]));

        await publisher.PublishApiHealthRunAsync(SampleApiHealthRequest() with
        {
            RunId = runId,
            Scope = "Service",
            ServiceName = "Tenant"
        });

        runName.Should().Be($"Link Automation UI - API Health Service Tenant - {hyphenated}");
        runName.Should().NotContain(compact);
    }

    [Fact]
    public async Task Scenario_failure_attaches_logs_when_response_has_only_id_and_test_id()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(8);
        api.Setup(a => a.AddResultsForCasesAsync(8, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TestRailResultDto { Id = 15433, TestId = 16898 }]);
        api.Setup(a => a.AddAttachmentToResultAsync(15433, It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var scenarioId = Guid.NewGuid();
        var publisher = CreatePublisher(api.Object, ConfiguredOptions(), ScenarioStore(scenarioId));

        await publisher.PublishScenarioRunAsync(SampleScenarioRequest() with
        {
            ScenarioId = scenarioId,
            Status = AutomationRunStatus.Failed,
            Error = "boom",
            Logs = ["line 1"]
        });

        api.Verify(a => a.AddAttachmentToResultAsync(
            15433,
            It.Is<string>(n => n.EndsWith(".log", StringComparison.Ordinal)),
            It.Is<byte[]>(b => Encoding.UTF8.GetString(b).Contains("boom")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Scenario_failure_logs_warning_when_no_result_matches()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(8);
        api.Setup(a => a.AddResultsForCasesAsync(8, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var scenarioId = Guid.NewGuid();
        var logs = new CaptureLogger<TestRailPublisher>();
        var publisher = CreatePublisher(api.Object, ConfiguredOptions(), ScenarioStore(scenarioId), logger: logs);

        await publisher.PublishScenarioRunAsync(SampleScenarioRequest() with
        {
            ScenarioId = scenarioId,
            Status = AutomationRunStatus.Failed,
            Error = "boom"
        });

        api.Verify(a => a.AddAttachmentToResultAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        logs.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Warning
            && entry.Message.Contains("attachment skipped for case C42", StringComparison.Ordinal));
    }

    [Fact]
    public void FindPostedResult_matches_by_case_then_by_position()
    {
        IReadOnlyList<TestRailResultDto> withCases =
        [
            new TestRailResultDto { Id = 10, CaseId = 2 },
            new TestRailResultDto { Id = 11, CaseId = 1 }
        ];
        TestRailPublisher.FindPostedResult(withCases, 2, 0, 1)!.Id.Should().Be(11);

        IReadOnlyList<TestRailResultDto> testIdsOnly =
        [
            new TestRailResultDto { Id = 20, TestId = 200 },
            new TestRailResultDto { Id = 21, TestId = 201 }
        ];
        TestRailPublisher.FindPostedResult(testIdsOnly, 2, 1, 7)!.Id.Should().Be(21);
        TestRailPublisher.FindPostedResult(testIdsOnly, 3, 1, 7).Should().BeNull();
        TestRailPublisher.FindPostedResult([], 1, 0, 7).Should().BeNull();
    }

    [Fact]
    public async Task UseSharedRun_creates_once_then_reuses()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(321);
        api.Setup(a => a.AddResultsForCasesAsync(321, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        api.Setup(a => a.GetRunAsync(321, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestRailRunDetails { Id = 321, SuiteId = 100, Name = TestRailPublisher.SharedScenarioRunName, IncludeAll = false });
        api.Setup(a => a.GetRunCaseIdsAsync(321, It.IsAny<CancellationToken>()))
            .ReturnsAsync([42]);
        var scenarioId = Guid.NewGuid();
        var opts = ConfiguredOptions();
        opts.UseSharedRun = true;
        var publisher = CreatePublisher(api.Object, opts, ScenarioStore(scenarioId));
        var request = SampleScenarioRequest() with { ScenarioId = scenarioId };

        await publisher.PublishScenarioRunAsync(request);
        await publisher.PublishScenarioRunAsync(request);

        api.Verify(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Once);
        api.Verify(a => a.GetRunAsync(321, It.IsAny<CancellationToken>()), Times.Once);
        api.Verify(a => a.AddResultsForCasesAsync(321, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        api.Verify(a => a.UpdateRunCaseIdsAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UseSharedRun_adds_later_case_ids_before_posting()
    {
        var api = new Mock<ITestRailApiClient>();
        IReadOnlyList<int>? updated = null;
        api.Setup(a => a.AddRunAsync(9, 100, It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(321);
        api.Setup(a => a.GetRunAsync(321, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestRailRunDetails { Id = 321, SuiteId = 100, Name = TestRailPublisher.SharedScenarioRunName, IncludeAll = false });
        // Case 50 was added to the run outside this publisher (another instance or by hand).
        api.Setup(a => a.GetRunCaseIdsAsync(321, It.IsAny<CancellationToken>()))
            .ReturnsAsync([42, 50]);
        api.Setup(a => a.UpdateRunCaseIdsAsync(321, It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .Callback<int, IReadOnlyList<int>, CancellationToken>((_, ids, _) => updated = ids)
            .Returns(Task.CompletedTask);
        api.Setup(a => a.AddResultsForCasesAsync(321, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var scenarios = new Mock<IScenarioStore>();
        scenarios.Setup(s => s.GetByIdAsync(firstId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestScenarioDefinition { Id = firstId, Name = "One", TestRailCaseId = 42 });
        scenarios.Setup(s => s.GetByIdAsync(secondId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestScenarioDefinition { Id = secondId, Name = "Two", TestRailCaseId = 43 });

        var opts = ConfiguredOptions();
        opts.UseSharedRun = true;
        var publisher = CreatePublisher(api.Object, opts, scenarios.Object);

        await publisher.PublishScenarioRunAsync(SampleScenarioRequest() with { ScenarioId = firstId });
        await publisher.PublishScenarioRunAsync(SampleScenarioRequest() with { ScenarioId = secondId });

        updated.Should().Equal(42, 43, 50);
        api.Verify(a => a.AddRunAsync(
            9,
            100,
            It.IsAny<string>(),
            It.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 42 })),
            It.IsAny<CancellationToken>()), Times.Once);
        api.Verify(a => a.AddResultsForCasesAsync(321, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task UseSharedRun_creates_one_run_when_two_publishes_overlap()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var addRunCalls = 0;
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                if (Interlocked.Increment(ref addRunCalls) == 1)
                {
                    entered.TrySetResult();
                    await release.Task;
                }

                return 321;
            });
        api.Setup(a => a.AddResultsForCasesAsync(321, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        api.Setup(a => a.GetRunAsync(321, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestRailRunDetails { Id = 321, SuiteId = 100, Name = TestRailPublisher.SharedScenarioRunName, IncludeAll = false });
        api.Setup(a => a.GetRunCaseIdsAsync(321, It.IsAny<CancellationToken>()))
            .ReturnsAsync([42]);

        var scenarioId = Guid.NewGuid();
        var opts = ConfiguredOptions();
        opts.UseSharedRun = true;
        var publisher = CreatePublisher(api.Object, opts, ScenarioStore(scenarioId));
        var request = SampleScenarioRequest() with { ScenarioId = scenarioId };

        var first = publisher.PublishScenarioRunAsync(request);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = publisher.PublishScenarioRunAsync(request);
        await Task.Delay(50);
        addRunCalls.Should().Be(1);

        release.TrySetResult();
        await Task.WhenAll(first, second);

        addRunCalls.Should().Be(1);
        api.Verify(a => a.AddResultsForCasesAsync(321, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Pinned_shared_run_unions_missing_cases_before_posting()
    {
        var cases = new HashSet<int> { 10 };
        var events = new List<string>();
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.GetRunAsync(999, It.IsAny<CancellationToken>()))
            .Callback(() => events.Add("get_run"))
            .ReturnsAsync(new TestRailRunDetails { Id = 999, IncludeAll = false, Name = "pinned" });
        api.Setup(a => a.GetRunCaseIdsAsync(999, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                events.Add("get_tests");
                return Task.FromResult<IReadOnlyList<int>>(cases.OrderBy(id => id).ToList());
            });
        api.Setup(a => a.UpdateRunCaseIdsAsync(999, It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .Callback<int, IReadOnlyList<int>, CancellationToken>((_, ids, _) =>
            {
                events.Add("update:" + string.Join(',', ids));
                cases.Clear();
                foreach (var id in ids)
                    cases.Add(id);
            })
            .Returns(Task.CompletedTask);
        api.Setup(a => a.AddResultsForCasesAsync(999, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .Callback(() => events.Add("post"))
            .ReturnsAsync([]);

        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var scenarios = new Mock<IScenarioStore>();
        scenarios.Setup(s => s.GetByIdAsync(firstId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestScenarioDefinition { Id = firstId, Name = "One", TestRailCaseId = 42 });
        scenarios.Setup(s => s.GetByIdAsync(secondId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestScenarioDefinition { Id = secondId, Name = "Two", TestRailCaseId = 43 });

        var opts = ConfiguredOptions();
        opts.UseSharedRun = true;
        opts.SharedScenarioRunId = 999;
        var publisher = CreatePublisher(api.Object, opts, scenarios.Object);

        await publisher.PublishScenarioRunAsync(SampleScenarioRequest() with { ScenarioId = firstId });
        await publisher.PublishScenarioRunAsync(SampleScenarioRequest() with { ScenarioId = secondId });
        await publisher.PublishScenarioRunAsync(SampleScenarioRequest() with { ScenarioId = firstId });

        events.Should().Equal(
            "get_run", "get_tests", "update:10,42", "post",
            "get_run", "get_tests", "update:10,42,43", "post",
            "get_run", "get_tests", "post");
        api.Verify(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Pinned_shared_run_with_include_all_does_not_update()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.GetRunAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestRailRunDetails { Id = 999, IncludeAll = true, Name = "pinned" });
        api.Setup(a => a.AddResultsForCasesAsync(999, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var scenarioId = Guid.NewGuid();
        var opts = ConfiguredOptions();
        opts.UseSharedRun = true;
        opts.SharedScenarioRunId = 999;
        var publisher = CreatePublisher(api.Object, opts, ScenarioStore(scenarioId));

        await publisher.PublishScenarioRunAsync(SampleScenarioRequest() with { ScenarioId = scenarioId });
        await publisher.PublishScenarioRunAsync(SampleScenarioRequest() with { ScenarioId = scenarioId });

        api.Verify(a => a.GetRunAsync(999, It.IsAny<CancellationToken>()), Times.Exactly(2));
        api.Verify(a => a.GetRunCaseIdsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        api.Verify(a => a.UpdateRunCaseIdsAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        api.Verify(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        api.Verify(a => a.AddResultsForCasesAsync(999, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Pinned_shared_run_does_not_post_when_get_run_fails()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.GetRunAsync(999, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("missing run"));

        var scenarioId = Guid.NewGuid();
        var opts = ConfiguredOptions();
        opts.UseSharedRun = true;
        opts.SharedScenarioRunId = 999;
        var publisher = CreatePublisher(api.Object, opts, ScenarioStore(scenarioId));

        await publisher.Invoking(p => p.PublishScenarioRunAsync(SampleScenarioRequest() with { ScenarioId = scenarioId }))
            .Should().NotThrowAsync();

        api.Verify(a => a.AddResultsForCasesAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()), Times.Never);
        api.Verify(a => a.UpdateRunCaseIdsAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UseSharedRun_reuses_an_existing_open_run_and_unions_its_cases()
    {
        var cases = new HashSet<int> { 7 };
        var updates = new List<int[]>();
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.GetOpenRunsAsync(9, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new TestRailRunDetails { Id = 4, SuiteId = 100, Name = TestRailPublisher.SharedScenarioRunName, IsCompleted = true },
                new TestRailRunDetails { Id = 1, SuiteId = 100, Name = "Link Automation UI - Scenario suite (old)" },
                new TestRailRunDetails { Id = 3, SuiteId = 999, Name = TestRailPublisher.SharedScenarioRunName },
                new TestRailRunDetails { Id = 777, SuiteId = 100, Name = TestRailPublisher.SharedScenarioRunName }
            ]);
        api.Setup(a => a.GetRunAsync(777, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestRailRunDetails
            {
                Id = 777,
                SuiteId = 100,
                Name = TestRailPublisher.SharedScenarioRunName,
                IncludeAll = false
            });
        api.Setup(a => a.GetRunCaseIdsAsync(777, It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<IReadOnlyList<int>>(cases.OrderBy(id => id).ToList()));
        api.Setup(a => a.UpdateRunCaseIdsAsync(777, It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .Callback<int, IReadOnlyList<int>, CancellationToken>((_, ids, _) =>
            {
                updates.Add(ids.ToArray());
                cases.Clear();
                foreach (var id in ids)
                    cases.Add(id);
            })
            .Returns(Task.CompletedTask);
        api.Setup(a => a.AddResultsForCasesAsync(777, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var thirdId = Guid.NewGuid();
        var scenarios = new Mock<IScenarioStore>();
        scenarios.Setup(s => s.GetByIdAsync(firstId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestScenarioDefinition { Id = firstId, Name = "One", TestRailCaseId = 42 });
        scenarios.Setup(s => s.GetByIdAsync(secondId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestScenarioDefinition { Id = secondId, Name = "Two", TestRailCaseId = 43 });
        scenarios.Setup(s => s.GetByIdAsync(thirdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestScenarioDefinition { Id = thirdId, Name = "Three", TestRailCaseId = 44 });

        var opts = ConfiguredOptions();
        opts.UseSharedRun = true;
        var firstPublisher = CreatePublisher(api.Object, opts, scenarios.Object);
        await firstPublisher.PublishScenarioRunAsync(SampleScenarioRequest() with { ScenarioId = firstId });
        await firstPublisher.PublishScenarioRunAsync(SampleScenarioRequest() with { ScenarioId = secondId });

        var restarted = CreatePublisher(api.Object, opts, scenarios.Object);
        await restarted.PublishScenarioRunAsync(SampleScenarioRequest() with { ScenarioId = thirdId });

        updates.Should().HaveCount(3);
        updates[0].Should().Equal(7, 42);
        updates[1].Should().Equal(7, 42, 43);
        updates[2].Should().Equal(7, 42, 43, 44);
        api.Verify(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        api.Verify(a => a.GetOpenRunsAsync(9, 100, It.IsAny<CancellationToken>()), Times.Exactly(2));
        api.Verify(a => a.GetRunAsync(777, It.IsAny<CancellationToken>()), Times.Exactly(3));
        api.Verify(a => a.AddResultsForCasesAsync(777, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task UseSharedRun_creates_when_no_open_run_has_the_shared_name()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.GetOpenRunsAsync(9, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new TestRailRunDetails { Id = 5, SuiteId = 100, Name = "someone else's run" },
                new TestRailRunDetails { Id = 6, SuiteId = 100, Name = TestRailPublisher.SharedScenarioRunName, IsCompleted = true }
            ]);
        api.Setup(a => a.AddRunAsync(9, 100, TestRailPublisher.SharedScenarioRunName, It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(321);
        api.Setup(a => a.AddResultsForCasesAsync(321, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        api.Setup(a => a.GetRunAsync(321, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestRailRunDetails { Id = 321, SuiteId = 100, Name = TestRailPublisher.SharedScenarioRunName, IncludeAll = false });
        api.Setup(a => a.GetRunCaseIdsAsync(321, It.IsAny<CancellationToken>()))
            .ReturnsAsync([42]);

        var scenarioId = Guid.NewGuid();
        var opts = ConfiguredOptions();
        opts.UseSharedRun = true;
        var publisher = CreatePublisher(api.Object, opts, ScenarioStore(scenarioId));
        var request = SampleScenarioRequest() with { ScenarioId = scenarioId };

        await publisher.PublishScenarioRunAsync(request);
        await publisher.PublishScenarioRunAsync(request);

        api.Verify(a => a.AddRunAsync(
            9,
            100,
            TestRailPublisher.SharedScenarioRunName,
            It.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 42 })),
            It.IsAny<CancellationToken>()), Times.Once);
        api.Verify(a => a.GetOpenRunsAsync(9, 100, It.IsAny<CancellationToken>()), Times.Once);
        api.Verify(a => a.GetRunAsync(321, It.IsAny<CancellationToken>()), Times.Once);
        api.Verify(a => a.UpdateRunCaseIdsAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        api.Verify(a => a.AddResultsForCasesAsync(321, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Pinned_shared_run_that_is_completed_does_not_post_or_update()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.GetRunAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestRailRunDetails { Id = 999, IncludeAll = false, IsCompleted = true, Name = "pinned" });

        var scenarioId = Guid.NewGuid();
        var opts = ConfiguredOptions();
        opts.UseSharedRun = true;
        opts.SharedScenarioRunId = 999;
        var publisher = CreatePublisher(api.Object, opts, ScenarioStore(scenarioId));

        await publisher.Invoking(p => p.PublishScenarioRunAsync(SampleScenarioRequest() with { ScenarioId = scenarioId }))
            .Should().NotThrowAsync();

        api.Verify(a => a.GetRunCaseIdsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        api.Verify(a => a.UpdateRunCaseIdsAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        api.Verify(a => a.AddResultsForCasesAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()), Times.Never);
        api.Verify(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UseSharedRun_replaces_a_cached_run_that_was_closed()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.GetOpenRunsAsync(9, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        api.SetupSequence(a => a.AddRunAsync(9, 100, TestRailPublisher.SharedScenarioRunName, It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(321)
            .ReturnsAsync(322);
        api.Setup(a => a.GetRunAsync(321, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestRailRunDetails { Id = 321, SuiteId = 100, Name = TestRailPublisher.SharedScenarioRunName, IsCompleted = true });
        api.Setup(a => a.AddResultsForCasesAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var scenarioId = Guid.NewGuid();
        var opts = ConfiguredOptions();
        opts.UseSharedRun = true;
        var publisher = CreatePublisher(api.Object, opts, ScenarioStore(scenarioId));
        var request = SampleScenarioRequest() with { ScenarioId = scenarioId };

        await publisher.PublishScenarioRunAsync(request);
        await publisher.PublishScenarioRunAsync(request);

        api.Verify(a => a.AddRunAsync(9, 100, TestRailPublisher.SharedScenarioRunName, It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        api.Verify(a => a.UpdateRunCaseIdsAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        api.Verify(a => a.AddResultsForCasesAsync(321, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()), Times.Once);
        api.Verify(a => a.AddResultsForCasesAsync(322, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UseSharedRun_does_not_create_when_lookup_fails()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.GetOpenRunsAsync(9, 100, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("down"));

        var scenarioId = Guid.NewGuid();
        var opts = ConfiguredOptions();
        opts.UseSharedRun = true;
        var publisher = CreatePublisher(api.Object, opts, ScenarioStore(scenarioId));

        await publisher.Invoking(p => p.PublishScenarioRunAsync(SampleScenarioRequest() with { ScenarioId = scenarioId }))
            .Should().NotThrowAsync();

        api.Verify(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        api.Verify(a => a.AddResultsForCasesAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UseSharedRun_keeps_separate_runs_when_scenario_and_api_health_share_a_suite()
    {
        var created = new List<string>();
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.GetOpenRunsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        api.Setup(a => a.AddRunAsync(9, 100, It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .Callback<int, int, string, IReadOnlyList<int>, CancellationToken>((_, _, name, _, _) => created.Add(name))
            .ReturnsAsync(50);
        api.Setup(a => a.AddResultsForCasesAsync(50, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var scenarioId = Guid.NewGuid();
        var opts = ConfiguredOptions();
        opts.UseSharedRun = true;
        opts.ApiHealthSuiteId = 100;
        var definition = new ApiEndpointDefinition
        {
            ServiceName = "Tenant",
            EndpointName = "Create Facility",
            TestRailCaseId = 8
        };
        var store = new Mock<IApiHealthRunStore>();
        store.Setup(s => s.GetLatestResultsForRunAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, ApiTestRunResult>
            {
                [definition.Key] = new()
                {
                    EndpointKey = definition.Key,
                    EndpointName = definition.EndpointName,
                    ServiceName = "Tenant",
                    Passed = true
                }
            });

        var publisher = CreatePublisher(
            api.Object,
            opts,
            ScenarioStore(scenarioId),
            store.Object,
            new ApiEndpointRegistry([new FakeSuite(definition)]));

        await publisher.PublishScenarioRunAsync(SampleScenarioRequest() with { ScenarioId = scenarioId });
        await publisher.PublishApiHealthRunAsync(SampleApiHealthRequest());

        created.Should().Equal(
            TestRailPublisher.SharedScenarioRunName,
            TestRailPublisher.SharedApiHealthRunName);
    }

    [Fact]
    public async Task Fail_open_when_add_run_throws()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("testrail down"));

        var scenarioId = Guid.NewGuid();
        var publisher = CreatePublisher(api.Object, ConfiguredOptions(), ScenarioStore(scenarioId));

        var act = () => publisher.PublishScenarioRunAsync(SampleScenarioRequest() with { ScenarioId = scenarioId });
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Fail_open_when_add_results_throws()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        api.Setup(a => a.AddResultsForCasesAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("500"));

        var scenarioId = Guid.NewGuid();
        var publisher = CreatePublisher(api.Object, ConfiguredOptions(), ScenarioStore(scenarioId));

        await publisher.Invoking(p => p.PublishScenarioRunAsync(SampleScenarioRequest() with { ScenarioId = scenarioId }))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task ApiHealth_maps_pass_fail_skip_and_creates_run()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.AddRunAsync(9, 200, It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(44);
        api.Setup(a => a.AddResultsForCasesAsync(44, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var passDef = new ApiEndpointDefinition
        {
            ServiceName = "Tenant",
            EndpointName = "Create Facility",
            TestRailCaseId = 1
        };
        var failDef = new ApiEndpointDefinition
        {
            ServiceName = "Tenant",
            EndpointName = "Get Facility",
            TestRailCaseId = 2
        };
        var skipDef = new ApiEndpointDefinition
        {
            ServiceName = "Tenant",
            EndpointName = "Skip Me",
            TestRailCaseId = 3
        };

        var store = new Mock<IApiHealthRunStore>();
        store.Setup(s => s.GetLatestResultsForRunAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, ApiTestRunResult>
            {
                [passDef.Key] = new() { EndpointKey = passDef.Key, EndpointName = passDef.EndpointName, ServiceName = "Tenant", Passed = true, DurationMs = 1500 },
                [failDef.Key] = new() { EndpointKey = failDef.Key, EndpointName = failDef.EndpointName, ServiceName = "Tenant", Passed = false, ErrorMessage = "nope" },
                [skipDef.Key] = new() { EndpointKey = skipDef.Key, EndpointName = skipDef.EndpointName, ServiceName = "Tenant", Skipped = true, SkipReason = "env" }
            });

        var publisher = CreatePublisher(
            api.Object,
            ConfiguredOptions(),
            apiHealthStore: store.Object,
            registry: new ApiEndpointRegistry([new FakeSuite(passDef, failDef, skipDef)]));

        await publisher.PublishApiHealthRunAsync(SampleApiHealthRequest());

        api.Verify(a => a.AddResultsForCasesAsync(
            44,
            It.Is<IReadOnlyList<TestRailCaseResult>>(results =>
                results.Count == 2
                && results.Single(r => r.CaseId == 1).StatusId == TestRailStatusMapper.Passed
                && results.Single(r => r.CaseId == 2).StatusId == TestRailStatusMapper.Failed
                && results.All(r => r.CaseId != 3)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApiHealth_same_case_posts_failed_when_endpoints_disagree()
    {
        var api = new Mock<ITestRailApiClient>();
        IReadOnlyList<int>? runCaseIds = null;
        api.Setup(a => a.AddRunAsync(9, 200, It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .Callback<int, int, string, IReadOnlyList<int>, CancellationToken>((_, _, _, caseIds, _) => runCaseIds = caseIds)
            .ReturnsAsync(44);
        IReadOnlyList<TestRailCaseResult>? posted = null;
        api.Setup(a => a.AddResultsForCasesAsync(44, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .Callback<int, IReadOnlyList<TestRailCaseResult>, CancellationToken>((_, results, _) => posted = results)
            .ReturnsAsync([]);

        var passDef = new ApiEndpointDefinition
        {
            ServiceName = "Tenant",
            EndpointName = "Create Facility",
            TestRailCaseId = 7
        };
        var failDef = new ApiEndpointDefinition
        {
            ServiceName = "Tenant",
            EndpointName = "Get Facility",
            TestRailCaseId = 7
        };
        var store = new Mock<IApiHealthRunStore>();
        store.Setup(s => s.GetLatestResultsForRunAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, ApiTestRunResult>
            {
                [passDef.Key] = new() { EndpointKey = passDef.Key, EndpointName = passDef.EndpointName, ServiceName = "Tenant", Passed = true },
                [failDef.Key] = new() { EndpointKey = failDef.Key, EndpointName = failDef.EndpointName, ServiceName = "Tenant", Passed = false, ErrorMessage = "nope" }
            });

        var publisher = CreatePublisher(
            api.Object,
            ConfiguredOptions(),
            apiHealthStore: store.Object,
            registry: new ApiEndpointRegistry([new FakeSuite(passDef, failDef)]));

        await publisher.PublishApiHealthRunAsync(SampleApiHealthRequest());

        runCaseIds.Should().Equal(7);
        var result = posted.Should().ContainSingle().Subject;
        result.CaseId.Should().Be(7);
        result.StatusId.Should().Be(TestRailStatusMapper.Failed);
        result.Comment.Should().Contain("Create Facility");
        result.Comment.Should().Contain("Get Facility");
        result.Comment.Should().Contain("Posted as Failed");
        result.AttachmentFileName.Should().Be("api-health-c7.txt");
    }

    [Fact]
    public async Task ApiHealth_same_case_keeps_the_shared_status_when_endpoints_agree()
    {
        var api = new Mock<ITestRailApiClient>();
        IReadOnlyList<TestRailCaseResult>? posted = null;
        api.Setup(a => a.AddRunAsync(9, 200, It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(44);
        api.Setup(a => a.AddResultsForCasesAsync(44, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .Callback<int, IReadOnlyList<TestRailCaseResult>, CancellationToken>((_, results, _) => posted = results)
            .ReturnsAsync([]);

        var first = new ApiEndpointDefinition { ServiceName = "Tenant", EndpointName = "Create Facility", TestRailCaseId = 7 };
        var second = new ApiEndpointDefinition { ServiceName = "Tenant", EndpointName = "List Facilities", TestRailCaseId = 7 };
        var store = new Mock<IApiHealthRunStore>();
        store.Setup(s => s.GetLatestResultsForRunAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, ApiTestRunResult>
            {
                [first.Key] = new() { EndpointKey = first.Key, EndpointName = first.EndpointName, ServiceName = "Tenant", Passed = true },
                [second.Key] = new() { EndpointKey = second.Key, EndpointName = second.EndpointName, ServiceName = "Tenant", Passed = true }
            });

        var publisher = CreatePublisher(
            api.Object,
            ConfiguredOptions(),
            apiHealthStore: store.Object,
            registry: new ApiEndpointRegistry([new FakeSuite(first, second)]));

        await publisher.PublishApiHealthRunAsync(SampleApiHealthRequest());

        var result = posted.Should().ContainSingle().Subject;
        result.StatusId.Should().Be(TestRailStatusMapper.Passed);
        result.Comment.Should().Contain("Create Facility");
        result.Comment.Should().Contain("List Facilities");
        result.Comment.Should().NotContain("Posted as Failed");
        result.Attachment.Should().BeNull();
    }

    [Fact]
    public async Task ApiHealth_omits_skipped_results()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        api.Setup(a => a.AddResultsForCasesAsync(3, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var skipDef = new ApiEndpointDefinition { ServiceName = "Tenant", EndpointName = "Skip Me", TestRailCaseId = 3 };
        var store = new Mock<IApiHealthRunStore>();
        store.Setup(s => s.GetLatestResultsForRunAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, ApiTestRunResult>
            {
                [skipDef.Key] = new() { EndpointKey = skipDef.Key, EndpointName = skipDef.EndpointName, Skipped = true }
            });

        var publisher = CreatePublisher(
            api.Object,
            ConfiguredOptions(),
            apiHealthStore: store.Object,
            registry: new ApiEndpointRegistry([new FakeSuite(skipDef)]));

        await publisher.PublishApiHealthRunAsync(SampleApiHealthRequest());

        api.Verify(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApiHealth_fail_open_when_store_throws()
    {
        var api = new Mock<ITestRailApiClient>(MockBehavior.Strict);
        var store = new Mock<IApiHealthRunStore>();
        store.Setup(s => s.GetLatestResultsForRunAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("mongo down"));

        var publisher = CreatePublisher(api.Object, ConfiguredOptions(), apiHealthStore: store.Object);
        await publisher.Invoking(p => p.PublishApiHealthRunAsync(SampleApiHealthRequest()))
            .Should().NotThrowAsync();
        api.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Slow_TestRail_is_logged_and_does_not_throw()
    {
        var handler = new DelayUntilCancelledHandler();
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(200) };
        var scenarioId = Guid.NewGuid();
        var options = ConfiguredOptions();
        var logs = new CaptureLogger<TestRailPublisher>();
        var publisher = CreatePublisher(
            new TestRailApiClient(http, options, NullLogger<TestRailApiClient>.Instance),
            options,
            ScenarioStore(scenarioId),
            logger: logs);

        var started = DateTime.UtcNow;
        var act = () => publisher.PublishScenarioRunAsync(SampleScenarioRequest() with
        {
            ScenarioId = scenarioId,
            Status = AutomationRunStatus.Failed,
            Error = "boom"
        });

        await act.Should().NotThrowAsync();
        (DateTime.UtcNow - started).Should().BeLessThan(TimeSpan.FromSeconds(2));
        logs.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Error
            && entry.Message.Contains("TestRail add_run failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Repeated_429_is_logged_and_does_not_throw()
    {
        var handler = new CountingHandler(_ => TooManyRequests());
        using var http = new HttpClient(handler);
        var scenarioId = Guid.NewGuid();
        var options = ConfiguredOptions();
        var logs = new CaptureLogger<TestRailPublisher>();
        var publisher = CreatePublisher(
            new TestRailApiClient(http, options, NullLogger<TestRailApiClient>.Instance),
            options,
            ScenarioStore(scenarioId),
            logger: logs);

        var act = () => publisher.PublishScenarioRunAsync(SampleScenarioRequest() with { ScenarioId = scenarioId });

        await act.Should().NotThrowAsync();
        handler.Count.Should().Be(2);
        logs.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Error
            && entry.Message.Contains("TestRail add_run failed", StringComparison.Ordinal));
    }

    [Fact]
    public void FormatElapsedMs_matches_testrail_style()
    {
        TestRailPublisher.FormatElapsedMs(400).Should().Be("1s");
        TestRailPublisher.FormatElapsedMs(1500).Should().Be("2s");
        TestRailPublisher.FormatElapsedMs(60_000).Should().Be("1m");
        TestRailPublisher.FormatElapsedMs(90_000).Should().Be("1m 30s");
    }

    [Fact]
    public async Task Saved_case_id_is_used_instead_of_the_run_name()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.AddRunAsync(9, 100, It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(501);
        api.Setup(a => a.AddResultsForCasesAsync(501, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TestRailResultDto { Id = 1, CaseId = 42 }]);

        var scenarioId = Guid.NewGuid();
        var opts = ConfiguredOptions();
        opts.ScenarioCaseIds["Smoke"] = 99;
        var publisher = CreatePublisher(api.Object, opts, ScenarioStore(scenarioId, 42));

        await publisher.PublishScenarioRunAsync(SampleScenarioRequest() with
        {
            ScenarioId = scenarioId,
            RunName = "Smoke",
            Status = AutomationRunStatus.Failed
        });

        api.Verify(a => a.AddRunAsync(
            9,
            100,
            It.IsAny<string>(),
            It.Is<IReadOnlyList<int>>(ids => ids.Single() == 42),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_name_alone_does_not_select_a_case()
    {
        var api = new Mock<ITestRailApiClient>(MockBehavior.Strict);
        var opts = ConfiguredOptions();
        opts.ScenarioCaseIds["Smoke"] = 42;
        var publisher = CreatePublisher(api.Object, opts);

        await publisher.PublishScenarioRunAsync(SampleScenarioRequest());

        api.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Each_publish_creates_its_own_run()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(11);
        api.Setup(a => a.AddResultsForCasesAsync(11, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var scenarioId = Guid.NewGuid();
        var publisher = CreatePublisher(api.Object, ConfiguredOptions(), ScenarioStore(scenarioId));
        var request = SampleScenarioRequest() with { ScenarioId = scenarioId };

        await publisher.PublishScenarioRunAsync(request);
        await publisher.PublishScenarioRunAsync(request with { RunId = Guid.NewGuid() });

        api.Verify(a => a.AddRunAsync(
            9,
            100,
            It.IsAny<string>(),
            It.Is<IReadOnlyList<int>>(ids => ids.Count == 1 && ids[0] == 42),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Cancelled_scenario_run_is_not_posted()
    {
        var api = new Mock<ITestRailApiClient>(MockBehavior.Strict);
        var scenarioId = Guid.NewGuid();
        var logs = new CaptureLogger<TestRailPublisher>();
        var publisher = CreatePublisher(api.Object, ConfiguredOptions(), ScenarioStore(scenarioId), logger: logs);

        await publisher.PublishScenarioRunAsync(SampleScenarioRequest() with
        {
            ScenarioId = scenarioId,
            Status = AutomationRunStatus.Cancelled
        });

        api.VerifyNoOtherCalls();
        logs.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Information
            && entry.Message.Contains("cancelled runs are not posted", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Cancelled_api_health_run_is_not_posted()
    {
        var api = new Mock<ITestRailApiClient>(MockBehavior.Strict);
        var logs = new CaptureLogger<TestRailPublisher>();
        var publisher = CreatePublisher(api.Object, ConfiguredOptions(), logger: logs);

        await publisher.PublishApiHealthRunAsync(SampleApiHealthRequest() with { Cancelled = true, Failed = true });

        api.VerifyNoOtherCalls();
        logs.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Information
            && entry.Message.Contains("cancelled runs are not posted", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Scenario_publish_records_the_created_run_and_single_test_link()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.AddRunAsync(9, 100, It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(501);
        api.Setup(a => a.AddResultsForCasesAsync(501, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TestRailResultDto { Id = 1, CaseId = 42, TestId = 16898 }]);

        var scenarioId = Guid.NewGuid();
        var options = ConfiguredOptions();
        options.BaseUrl = "https://example.testrail.io///";
        var publisher = CreatePublisher(api.Object, options, ScenarioStore(scenarioId));
        var request = SampleScenarioRequest() with { ScenarioId = scenarioId, Status = AutomationRunStatus.Succeeded };

        await publisher.PublishScenarioRunAsync(request);

        var published = request.PublishedRuns.Should().ContainSingle().Subject;
        published.RunId.Should().Be(501);
        published.Url.Should().Be("https://example.testrail.io/index.php?/runs/view/501");
        published.TestId.Should().Be(16898);
        published.TestUrl.Should().Be("https://example.testrail.io/index.php?/tests/view/16898");
    }

    [Fact]
    public async Task Shared_scenario_publish_records_the_existing_run()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.GetOpenRunsAsync(9, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new TestRailRunDetails
                {
                    Id = 777,
                    SuiteId = 100,
                    Name = TestRailPublisher.SharedScenarioRunName,
                    IncludeAll = true
                }
            ]);
        api.Setup(a => a.GetRunAsync(777, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestRailRunDetails
            {
                Id = 777,
                SuiteId = 100,
                Name = TestRailPublisher.SharedScenarioRunName,
                IncludeAll = true
            });
        api.Setup(a => a.AddResultsForCasesAsync(777, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TestRailResultDto { Id = 9, CaseId = 42, TestId = 91 }]);

        var scenarioId = Guid.NewGuid();
        var options = ConfiguredOptions();
        options.UseSharedRun = true;
        options.BaseUrl = "https://example.testrail.io/";
        var publisher = CreatePublisher(api.Object, options, ScenarioStore(scenarioId));
        var request = SampleScenarioRequest() with { ScenarioId = scenarioId };

        await publisher.PublishScenarioRunAsync(request);

        api.Verify(a => a.AddRunAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        var published = request.PublishedRuns.Should().ContainSingle().Subject;
        published.RunId.Should().Be(777);
        published.Url.Should().Be("https://example.testrail.io/index.php?/runs/view/777");
        published.TestUrl.Should().Be("https://example.testrail.io/index.php?/tests/view/91");
    }

    [Fact]
    public async Task Api_health_publish_records_the_run_without_a_single_test_link()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.AddRunAsync(9, 200, It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(44);
        api.Setup(a => a.AddResultsForCasesAsync(44, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new TestRailResultDto { Id = 1, CaseId = 1, TestId = 10 },
                new TestRailResultDto { Id = 2, CaseId = 2, TestId = 11 }
            ]);

        var passDef = new ApiEndpointDefinition { ServiceName = "Tenant", EndpointName = "Create Facility", TestRailCaseId = 1 };
        var failDef = new ApiEndpointDefinition { ServiceName = "Tenant", EndpointName = "Get Facility", TestRailCaseId = 2 };
        var store = new Mock<IApiHealthRunStore>();
        store.Setup(s => s.GetLatestResultsForRunAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, ApiTestRunResult>
            {
                [passDef.Key] = new() { EndpointKey = passDef.Key, EndpointName = passDef.EndpointName, ServiceName = "Tenant", Passed = true },
                [failDef.Key] = new() { EndpointKey = failDef.Key, EndpointName = failDef.EndpointName, ServiceName = "Tenant", Passed = false, ErrorMessage = "nope" }
            });

        var options = ConfiguredOptions();
        options.BaseUrl = "https://example.testrail.io";
        var publisher = CreatePublisher(
            api.Object,
            options,
            apiHealthStore: store.Object,
            registry: new ApiEndpointRegistry([new FakeSuite(passDef, failDef)]));
        var request = SampleApiHealthRequest();

        await publisher.PublishApiHealthRunAsync(request);

        var published = request.PublishedRuns.Should().ContainSingle().Subject;
        published.RunId.Should().Be(44);
        published.Url.Should().Be("https://example.testrail.io/index.php?/runs/view/44");
        published.TestId.Should().BeNull();
        published.TestUrl.Should().BeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unpublished_scenario_run_records_nothing(bool cancelled)
    {
        var api = new Mock<ITestRailApiClient>(MockBehavior.Strict);
        var options = ConfiguredOptions();
        if (!cancelled)
            options.Enabled = false;
        var publisher = CreatePublisher(api.Object, options);
        var request = SampleScenarioRequest() with
        {
            Status = cancelled ? AutomationRunStatus.Cancelled : AutomationRunStatus.Succeeded
        };

        await publisher.PublishScenarioRunAsync(request);

        request.PublishedRuns.Should().BeEmpty();
        api.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Failed_result_post_does_not_record_a_run_link()
    {
        var api = new Mock<ITestRailApiClient>();
        api.Setup(a => a.AddRunAsync(9, 100, It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(501);
        api.Setup(a => a.AddResultsForCasesAsync(501, It.IsAny<IReadOnlyList<TestRailCaseResult>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));

        var scenarioId = Guid.NewGuid();
        var publisher = CreatePublisher(api.Object, ConfiguredOptions(), ScenarioStore(scenarioId));
        var request = SampleScenarioRequest() with { ScenarioId = scenarioId, Status = AutomationRunStatus.Failed };

        await publisher.PublishScenarioRunAsync(request);

        request.PublishedRuns.Should().BeEmpty();
    }

    private static IScenarioStore ScenarioStore(Guid scenarioId, int caseId = 42)
    {
        var scenarios = new Mock<IScenarioStore>();
        scenarios.Setup(s => s.GetByIdAsync(scenarioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestScenarioDefinition
            {
                Id = scenarioId,
                Name = "Smoke",
                TestRailCaseId = caseId
            });
        return scenarios.Object;
    }

    private static TestRailPublisher CreatePublisher(
        ITestRailApiClient api,
        TestRailOptions opts,
        IScenarioStore? scenarios = null,
        IApiHealthRunStore? apiHealthStore = null,
        ApiEndpointRegistry? registry = null,
        ILogger<TestRailPublisher>? logger = null)
    {
        return new TestRailPublisher(
            Options.Create(opts),
            api,
            scenarios ?? Mock.Of<IScenarioStore>(),
            apiHealthStore ?? Mock.Of<IApiHealthRunStore>(),
            registry ?? new ApiEndpointRegistry([]),
            logger ?? NullLogger<TestRailPublisher>.Instance);
    }

    private static TestRailOptions ConfiguredOptions() => new()
    {
        Enabled = true,
        BaseUrl = "https://example.testrail.io",
        Username = "qa@example.com",
        ApiKey = "placeholder-key",
        ProjectId = 9,
        ScenarioSuiteId = 100,
        ApiHealthSuiteId = 200
    };

    private static ScenarioTestRailPublishRequest SampleScenarioRequest() => new()
    {
        RunId = Guid.NewGuid(),
        RunName = "Smoke",
        Status = AutomationRunStatus.Succeeded,
        StartedAt = DateTimeOffset.UtcNow.AddSeconds(-5),
        FinishedAt = DateTimeOffset.UtcNow
    };

    private static ApiHealthTestRailPublishRequest SampleApiHealthRequest() => new()
    {
        RunId = Guid.NewGuid(),
        Scope = "Service",
        ServiceName = "Tenant",
        StartedAt = DateTimeOffset.UtcNow.AddSeconds(-5),
        FinishedAt = DateTimeOffset.UtcNow
    };

    private static HttpResponseMessage TooManyRequests()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
        return response;
    }

    private sealed class DelayUntilCancelledHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class CountingHandler(Func<int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Count { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Count++;
            return Task.FromResult(respond(Count));
        }
    }

    private sealed class CaptureLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private sealed class FakeSuite(params ApiEndpointDefinition[] defs) : IServiceTestSuite
    {
        public string ServiceName => defs.FirstOrDefault()?.ServiceName ?? "Fake";
        public IReadOnlyList<ApiEndpointDefinition> GetEndpointDefinitions() => defs;
        public Task<IReadOnlyList<ApiTestRunResult>> ExecuteAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ApiTestRunResult>>([]);
        public IReadOnlyList<ApiHealthSeedRequirement> GetSeedRequirements() => [];
    }
}

[Trait("Category", "UnitTests")]
public class TestRailApiClientTests
{
    [Fact]
    public async Task AddRun_posts_basic_auth_and_case_ids()
    {
        var handler = new ScriptedHandler
        {
            Responder = (_, _) => Json(HttpStatusCode.OK, """{"id": 55}""")
        };
        var client = CreateClient(handler);

        var runId = await client.AddRunAsync(9, 100, "run-name", [1, 2]);

        runId.Should().Be(55);
        handler.Requests.Should().ContainSingle();
        var (request, body) = handler.Requests[0];
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri!.ToString().Should().Be("https://example.testrail.io/index.php?/api/v2/add_run/9");
        request.Headers.Authorization.Should().BeEquivalentTo(new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes("qa@example.com:placeholder-key"))));

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("suite_id").GetInt32().Should().Be(100);
        doc.RootElement.GetProperty("include_all").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("case_ids").EnumerateArray().Select(e => e.GetInt32()).Should().Equal(1, 2);
    }

    [Fact]
    public async Task UpdateRunCaseIds_posts_the_full_selective_set()
    {
        var handler = new ScriptedHandler
        {
            Responder = (_, _) => Json(HttpStatusCode.OK, """{"id": 55}""")
        };
        var client = CreateClient(handler);

        await client.UpdateRunCaseIdsAsync(55, [1, 2]);

        handler.Requests.Should().ContainSingle();
        var (request, body) = handler.Requests[0];
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri!.ToString().Should().Be("https://example.testrail.io/index.php?/api/v2/update_run/55");
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("include_all").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("case_ids").EnumerateArray().Select(e => e.GetInt32()).Should().Equal(1, 2);
        doc.RootElement.TryGetProperty("suite_id", out _).Should().BeFalse();
    }

    [Fact]
    public async Task AddResultsForCases_posts_status_map()
    {
        var handler = new ScriptedHandler
        {
            Responder = (_, _) => Json(HttpStatusCode.OK, """[{"id": 10, "case_id": 1}]""")
        };
        var client = CreateClient(handler);

        var posted = await client.AddResultsForCasesAsync(55,
        [
            new TestRailCaseResult { CaseId = 1, StatusId = 5, Comment = "failed" }
        ]);

        posted.Should().ContainSingle(r => r.Id == 10 && r.CaseId == 1);
        var body = handler.Requests[0].Body;
        using var doc = JsonDocument.Parse(body);
        var result = doc.RootElement.GetProperty("results")[0];
        result.GetProperty("case_id").GetInt32().Should().Be(1);
        result.GetProperty("status_id").GetInt32().Should().Be(5);
        result.GetProperty("comment").GetString().Should().Be("failed");
    }

    [Fact]
    public async Task AddResultsForCases_maps_case_ids_by_position_when_response_has_only_id_and_test_id()
    {
        var handler = new ScriptedHandler
        {
            Responder = (_, _) => Json(
                HttpStatusCode.OK,
                """[{"id": 15433, "test_id": 16898, "status_id": 5}, {"id": 15434, "test_id": 16899, "status_id": 1}]""")
        };
        var client = CreateClient(handler);

        var posted = await client.AddResultsForCasesAsync(269,
        [
            new TestRailCaseResult { CaseId = 13407, StatusId = 5, Comment = "failed" },
            new TestRailCaseResult { CaseId = 13408, StatusId = 1 }
        ]);

        posted.Should().HaveCount(2);
        posted[0].Should().BeEquivalentTo(new TestRailResultDto { Id = 15433, TestId = 16898, CaseId = 13407 });
        posted[1].Should().BeEquivalentTo(new TestRailResultDto { Id = 15434, TestId = 16899, CaseId = 13408 });
    }

    [Fact]
    public async Task Non_success_throws()
    {
        var handler = new ScriptedHandler
        {
            Responder = (_, _) => Json(HttpStatusCode.InternalServerError, """{"error": "nope"}""")
        };
        var client = CreateClient(handler);

        await client.Invoking(c => c.AddRunAsync(9, 100, "x", [1]))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*500*");
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task Failure_body_is_capped_and_credentials_are_redacted()
    {
        const string apiKey = "placeholder-key";
        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($"qa@example.com:{apiKey}"));
        var tail = new string('Z', 800);
        var handler = new ScriptedHandler
        {
            Responder = (_, _) => Json(
                HttpStatusCode.InternalServerError,
                "{\"error\":\"nope " + apiKey + " " + token + " " + tail + "\"}")
        };
        var client = CreateClient(handler);

        var ex = await client.Invoking(c => c.AddRunAsync(9, 100, "x", [1]))
            .Should().ThrowAsync<InvalidOperationException>();

        ex.Which.Message.Should().Contain("add_run/9");
        ex.Which.Message.Should().Contain("500");
        ex.Which.Message.Should().Contain("[redacted]");
        ex.Which.Message.Should().NotContain(apiKey);
        ex.Which.Message.Should().NotContain(token);
        ex.Which.Message.Should().NotContain(tail);
        ex.Which.Message.Length.Should().BeLessThan(TestRailApiClient.MaxFailureBodyChars + 80);
    }

    [Fact]
    public async Task Too_many_requests_then_success_retries_once()
    {
        var calls = 0;
        var handler = new ScriptedHandler
        {
            Responder = (_, _) =>
            {
                calls++;
                if (calls == 1)
                {
                    var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                    limited.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(1));
                    return limited;
                }

                return Json(HttpStatusCode.OK, """{"id": 55}""");
            }
        };
        var client = CreateClient(handler);

        var runId = await client.AddRunAsync(9, 100, "run-name", [1]);

        runId.Should().Be(55);
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task Retry_after_is_capped()
    {
        var delays = new List<TimeSpan>();
        var calls = 0;
        var handler = new ScriptedHandler
        {
            Responder = (_, _) =>
            {
                calls++;
                if (calls == 1)
                {
                    var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                    limited.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(120));
                    return limited;
                }

                return Json(HttpStatusCode.OK, """{"id": 7}""");
            }
        };
        var http = new HttpClient(handler);
        var client = new TestRailApiClient(
            () => http,
            new TestRailOptions
            {
                BaseUrl = "https://example.testrail.io",
                Username = "qa@example.com",
                ApiKey = "placeholder-key"
            },
            NullLogger<TestRailApiClient>.Instance,
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });

        var runId = await client.AddRunAsync(9, 100, "run-name", [1]);

        runId.Should().Be(7);
        delays.Should().Equal(TestRailApiClient.MaxRetryAfter);
    }

    [Fact]
    public async Task Too_many_requests_twice_gives_up()
    {
        var handler = new ScriptedHandler
        {
            Responder = (_, _) =>
            {
                var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                limited.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                return limited;
            }
        };
        var client = CreateClient(handler);

        await client.Invoking(c => c.AddRunAsync(9, 100, "x", [1]))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*429*");
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task AddAttachment_sends_multipart()
    {
        var handler = new ScriptedHandler
        {
            Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"attachment_id": 1}""", Encoding.UTF8, "application/json")
            }
        };
        var client = CreateClient(handler);

        await client.AddAttachmentToResultAsync(10, "fail.log", "hello"u8.ToArray());

        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Request.RequestUri!.ToString()
            .Should().Be("https://example.testrail.io/index.php?/api/v2/add_attachment_to_result/10");
        handler.Requests[0].Request.Content!.Headers.ContentType!.MediaType.Should().Be("multipart/form-data");
    }

    private static TestRailApiClient CreateClient(ScriptedHandler handler)
    {
        var http = new HttpClient(handler);
        var options = new TestRailOptions
        {
            BaseUrl = "https://example.testrail.io",
            Username = "qa@example.com",
            ApiKey = "placeholder-key"
        };
        return new TestRailApiClient(http, options, NullLogger<TestRailApiClient>.Instance);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];
        public Func<HttpRequestMessage, string, HttpResponseMessage> Responder { get; set; } =
            (_, _) => new HttpResponseMessage(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request, body));
            return Responder(request, body);
        }
    }
}
