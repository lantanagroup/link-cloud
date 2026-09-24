using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class LeftoverRunCleanupServiceTests
{
    [Fact]
    public async Task HistoryPurge_is_running_before_settings_are_read()
    {
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var run = Run(Guid.NewGuid().ToString(), now.AddDays(-30));
        var statuses = new List<string>();
        var service = Create(now, [run], [], statusesWhenSettingsLoad: statuses);

        await service.RunHistoryPurgeNowAsync();

        statuses.Should().Contain("running");
    }

    [Fact]
    public async Task HistoryPurge_records_guid_facility_teardown_and_skips_named_facilities()
    {
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var guidFacility = Guid.NewGuid().ToString();
        var guidRun = Run(guidFacility, now.AddDays(-30));
        var namedRun = Run("CityHospital", now.AddDays(-30));
        var saved = new List<CleanupReport>();
        var order = new List<string>();
        var service = Create(now, [guidRun, namedRun], saved, order: order);

        var result = await service.RunHistoryPurgeNowAsync();

        order.Should().ContainInOrder("save", "publish");
        result.TornDownFacilityIds.Should().BeEquivalentTo([guidFacility, guidRun.RunId.ToString(), namedRun.RunId.ToString()]);
        result.TornDownFacilityIds.Should().NotContain("CityHospital");
        result.TeardownCandidateCount.Should().Be(3);
        result.PurgedRunIds.Should().BeEquivalentTo([guidRun.RunId, namedRun.RunId]);
        saved.Should().ContainSingle();
        saved[0].TornDownFacilityIds.Should().BeEquivalentTo([guidFacility, guidRun.RunId.ToString(), namedRun.RunId.ToString()]);
        saved[0].TeardownCandidateCount.Should().Be(3);
        saved[0].PurgedRunIds.Should().BeEquivalentTo([guidRun.RunId, namedRun.RunId]);
    }

    [Fact]
    public async Task CustomRange_counts_history_teardown_when_facility_is_already_gone()
    {
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var guidFacility = Guid.NewGuid().ToString();
        var finished = now.AddDays(-2);
        var run = Run(guidFacility, finished);
        var saved = new List<CleanupReport>();
        var service = Create(now, [run], saved, facilities: new Dictionary<string, string>());

        var result = await service.RunCustomRangeAsync(
            finished.AddHours(-1),
            finished.AddHours(1),
            teardownFacilities: true,
            purgeHistory: true);

        result.TornDownFacilityIds.Should().BeEquivalentTo([guidFacility, run.RunId.ToString()]);
        result.TeardownCandidateCount.Should().Be(2);
        saved.Single().TeardownCandidateCount.Should().Be(2);
    }

    [Fact]
    public async Task CustomRange_clears_a_facility_failure_when_the_history_retry_succeeds()
    {
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var guidFacility = Guid.NewGuid().ToString();
        var finished = now.AddDays(-2);
        var run = Run(guidFacility, finished);
        var saved = new List<CleanupReport>();
        var service = Create(now, [run], saved, failFirstFacilityDeletes: 1);

        var result = await service.RunCustomRangeAsync(
            finished.AddHours(-1),
            finished.AddHours(1),
            teardownFacilities: true,
            purgeHistory: true);

        result.TornDownFacilityIds.Should().BeEquivalentTo([guidFacility, run.RunId.ToString()]);
        result.FailedFacilityIds.Should().BeEmpty();
        result.FailedRunIds.Should().BeEmpty();
        result.PurgedRunIds.Should().Equal(run.RunId);
        saved.Should().ContainSingle();
        saved[0].Status.Should().Be("completed");
        saved[0].FailedFacilityIds.Should().BeEmpty();
        saved[0].TornDownFacilityIds.Should().BeEquivalentTo([guidFacility, run.RunId.ToString()]);
    }

    [Fact]
    public async Task CompletedReport_is_not_duplicated_when_terminal_publish_is_cancelled()
    {
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var run = Run(Guid.NewGuid().ToString(), now.AddDays(-30));
        var saved = new List<CleanupReport>();
        var service = Create(now, [run], saved, throwOnTerminalPublish: true);

        var result = await service.RunHistoryPurgeNowAsync();

        result.PurgedRunIds.Should().Equal(run.RunId);
        saved.Should().ContainSingle();
        saved[0].Status.Should().Be("completed");
    }

    [Fact]
    public async Task HistoryPurge_keeps_the_run_when_facility_teardown_fails()
    {
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var guidFacility = Guid.NewGuid().ToString();
        var run = Run(guidFacility, now.AddDays(-30));
        var saved = new List<CleanupReport>();
        var service = Create(now, [run], saved, failFacilityDelete: true);

        var result = await service.RunHistoryPurgeNowAsync();

        result.TornDownFacilityIds.Should().BeEmpty();
        result.FailedFacilityIds.Should().BeEquivalentTo([guidFacility, run.RunId.ToString()]);
        result.FailedRunIds.Should().Equal(run.RunId);
        result.PurgedRunIds.Should().BeEmpty();
        saved.Single().PurgedRunIds.Should().BeEmpty();
        saved.Single().FailedRunIds.Should().Equal(run.RunId);
    }

    [Fact]
    public async Task HistoryPurge_tears_down_a_facility_identified_by_the_run_id()
    {
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var runId = Guid.NewGuid();
        var run = new AutomationRunSummary
        {
            RunId = runId,
            FacilityId = "CityHospital",
            ReportId = Guid.NewGuid().ToString(),
            Status = AutomationRunStatus.Succeeded,
            FinishedAt = now.AddDays(-30)
        };
        var saved = new List<CleanupReport>();
        var service = Create(now, [run], saved);

        var result = await service.RunHistoryPurgeNowAsync();

        result.TornDownFacilityIds.Should().Equal(runId.ToString());
        result.TeardownCandidateCount.Should().Be(1);
        result.PurgedRunIds.Should().Equal(runId);
        saved.Single().TornDownFacilityIds.Should().Equal(runId.ToString());
    }

    [Fact]
    public async Task CustomRange_does_not_delete_a_named_facility_after_the_run_id_is_torn_down()
    {
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var runId = Guid.NewGuid();
        var finished = now.AddDays(-2);
        var run = new AutomationRunSummary
        {
            RunId = runId,
            FacilityId = "CityHospital",
            ReportId = Guid.NewGuid().ToString(),
            Status = AutomationRunStatus.Succeeded,
            FinishedAt = finished
        };
        var deleted = new List<string>();
        var facilities = new Dictionary<string, string>
        {
            ["CityHospital"] = "City Hospital",
            [runId.ToString()] = "scenario"
        };
        var service = Create(now, [run], [], facilities: facilities, deletedFacilityIds: deleted);

        var result = await service.RunCustomRangeAsync(
            finished.AddHours(-1),
            finished.AddHours(1),
            teardownFacilities: true,
            purgeHistory: true);

        deleted.Should().Equal(runId.ToString());
        result.TornDownFacilityIds.Should().Equal(runId.ToString());
        result.PurgedRunIds.Should().Equal(runId);
    }

    [Fact]
    public async Task HistoryPurge_does_not_tear_down_a_preexisting_guid_tenant()
    {
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var tenantId = Guid.NewGuid().ToString();
        var run = new AutomationRunSummary
        {
            RunId = Guid.NewGuid(),
            FacilityId = tenantId,
            AutomationCreatedFacility = false,
            ReportId = Guid.NewGuid().ToString(),
            Status = AutomationRunStatus.Succeeded,
            FinishedAt = now.AddDays(-30)
        };
        var deleted = new List<string>();
        var service = Create(now, [run], [], deletedFacilityIds: deleted);

        var result = await service.RunHistoryPurgeNowAsync();

        deleted.Should().Equal(run.RunId.ToString());
        result.TornDownFacilityIds.Should().Equal(run.RunId.ToString());
        result.PurgedRunIds.Should().Equal(run.RunId);
    }

    [Fact]
    public async Task HistoryPurge_leaves_a_created_facility_while_another_run_is_active()
    {
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var shared = Guid.NewGuid().ToString();
        var creator = new AutomationRunSummary
        {
            RunId = Guid.NewGuid(),
            FacilityId = shared,
            AutomationCreatedFacility = true,
            ReportId = Guid.NewGuid().ToString(),
            Status = AutomationRunStatus.Succeeded,
            FinishedAt = now.AddDays(-30)
        };
        var active = new AutomationRunSummary
        {
            RunId = Guid.NewGuid(),
            FacilityId = shared,
            Status = AutomationRunStatus.Running,
            StartedAt = now.AddMinutes(-5)
        };
        var deleted = new List<string>();
        var facilities = new Dictionary<string, string> { [shared] = "shared" };
        var service = Create(now, [creator, active], [], facilities: facilities, deletedFacilityIds: deleted);

        var result = await service.RunHistoryPurgeNowAsync();

        deleted.Should().Equal(creator.RunId.ToString());
        result.TornDownFacilityIds.Should().Equal(creator.RunId.ToString());
        result.PurgedRunIds.Should().Equal(creator.RunId);
    }

    [Fact]
    public async Task HistoryPurge_leaves_a_created_facility_a_newer_terminal_run_still_uses()
    {
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var shared = Guid.NewGuid().ToString();
        var creator = new AutomationRunSummary
        {
            RunId = Guid.NewGuid(),
            FacilityId = shared,
            AutomationCreatedFacility = true,
            ReportId = Guid.NewGuid().ToString(),
            Status = AutomationRunStatus.Succeeded,
            FinishedAt = now.AddDays(-30)
        };
        var newer = new AutomationRunSummary
        {
            RunId = Guid.NewGuid(),
            FacilityId = shared,
            ReportId = Guid.NewGuid().ToString(),
            Status = AutomationRunStatus.Succeeded,
            FinishedAt = now.AddDays(-1)
        };
        var deleted = new List<string>();
        var facilities = new Dictionary<string, string> { [shared] = "shared" };
        var service = Create(now, [creator, newer], [], facilities: facilities, deletedFacilityIds: deleted);

        var result = await service.RunHistoryPurgeNowAsync();

        deleted.Should().Equal(creator.RunId.ToString());
        result.TornDownFacilityIds.Should().Equal(creator.RunId.ToString());
        result.PurgedRunIds.Should().Equal(creator.RunId);
    }

    [Fact]
    public async Task HistoryPurge_tears_down_a_facility_retained_after_its_run_was_deleted()
    {
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var retained = Guid.NewGuid().ToString();
        var released = new List<string>();
        var deleted = new List<string>();
        var facilities = new Dictionary<string, string> { [retained] = "retained" };
        var service = Create(
            now,
            [],
            [],
            facilities: facilities,
            deletedFacilityIds: deleted,
            retainedFacilityIds: [retained],
            releasedFacilityIds: released);

        var result = await service.RunHistoryPurgeNowAsync();

        deleted.Should().Equal(retained);
        result.TornDownFacilityIds.Should().Equal(retained);
        released.Should().Equal(retained);
        result.TeardownCandidateCount.Should().Be(1);
    }

    [Fact]
    public async Task CustomRange_does_not_tear_down_an_unrelated_retained_facility()
    {
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var retainedId = Guid.NewGuid().ToString();
        var deleted = new List<string>();
        var released = new List<string>();
        var facilities = new Dictionary<string, string> { [retainedId] = "retained" };
        var service = Create(
            now,
            [],
            [],
            facilities: facilities,
            deletedFacilityIds: deleted,
            retainedFacilityIds: [retainedId],
            releasedFacilityIds: released);

        var result = await service.RunCustomRangeAsync(
            now.AddDays(-2),
            now.AddDays(-1),
            teardownFacilities: true,
            purgeHistory: false);

        deleted.Should().BeEmpty();
        released.Should().BeEmpty();
        result.TornDownFacilityIds.Should().BeEmpty();
    }

    [Fact]
    public async Task CustomRange_history_only_does_not_count_facility_teardown()
    {
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var run = Run(Guid.NewGuid().ToString(), now.AddDays(-2));
        var service = Create(now, [run], []);

        var result = await service.RunCustomRangeAsync(
            now.AddDays(-3),
            now.AddDays(-1),
            teardownFacilities: false,
            purgeHistory: true);

        result.TornDownFacilityIds.Should().BeEmpty();
        result.TeardownCandidateCount.Should().Be(0);
        result.PurgedRunIds.Should().Equal(run.RunId);
        result.ProcessedAllCandidates.Should().BeTrue();
    }

    [Fact]
    public async Task CustomRange_history_only_does_not_partially_tear_down_when_the_cap_is_one()
    {
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var facilityId = Guid.NewGuid().ToString();
        var run = Run(facilityId, now.AddDays(-2));
        var deleted = new List<string>();
        var service = Create(now, [run], [], deletedFacilityIds: deleted, maxFacilitiesPerPass: 1);

        var result = await service.RunCustomRangeAsync(
            now.AddDays(-3),
            now.AddDays(-1),
            teardownFacilities: false,
            purgeHistory: true);

        deleted.Should().BeEmpty();
        result.TornDownFacilityIds.Should().BeEmpty();
        result.PurgedRunIds.Should().Equal(run.RunId);
    }

    [Fact]
    public async Task HistoryPurge_does_not_exceed_the_facility_cap_on_the_first_run()
    {
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var firstFacility = Guid.NewGuid().ToString();
        var secondFacility = Guid.NewGuid().ToString();
        var first = Run(firstFacility, now.AddDays(-30));
        var second = Run(secondFacility, now.AddDays(-30));
        var deleted = new List<string>();
        var service = Create(
            now,
            [first, second],
            [],
            deletedFacilityIds: deleted,
            maxFacilitiesPerPass: 1);

        var result = await service.RunHistoryPurgeNowAsync();

        deleted.Should().Equal(firstFacility);
        result.PurgedRunIds.Should().BeEmpty();
        result.ProcessedAllCandidates.Should().BeFalse();
    }

    [Fact]
    public async Task Teardown_skips_a_retained_facility_younger_than_retention()
    {
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var retainedId = Guid.NewGuid().ToString();
        var deleted = new List<string>();
        var facilities = new Dictionary<string, string> { [retainedId] = "retained" };
        var service = Create(
            now,
            [],
            [],
            facilities: facilities,
            deletedFacilityIds: deleted,
            retainedFacilityIds: [retainedId],
            retainedEligibleAt: now.AddDays(-1));

        var result = await service.RunOnceAsync();

        deleted.Should().BeEmpty();
        result.TornDownFacilityIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Teardown_does_not_let_an_old_finished_run_shield_a_retained_facility()
    {
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var retainedId = Guid.NewGuid().ToString();
        var oldRun = new AutomationRunSummary
        {
            RunId = Guid.NewGuid(),
            FacilityId = retainedId,
            Status = AutomationRunStatus.Succeeded,
            FinishedAt = now.AddDays(-30)
        };
        var deleted = new List<string>();
        var facilities = new Dictionary<string, string> { [retainedId] = "retained" };
        var service = Create(
            now,
            [oldRun],
            [],
            facilities: facilities,
            deletedFacilityIds: deleted,
            retainedFacilityIds: [retainedId],
            retainedEligibleAt: now.AddDays(-30));

        var result = await service.RunOnceAsync();

        deleted.Should().Contain(retainedId);
        result.TornDownFacilityIds.Should().Contain(retainedId);
    }

    [Fact]
    public async Task HistoryPurge_says_so_when_the_report_cannot_be_saved()
    {
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var run = Run(Guid.NewGuid().ToString(), now.AddDays(-30));
        var messages = new List<string>();
        var service = Create(now, [run], [], failReportSave: true, terminalMessages: messages);

        var result = await service.RunHistoryPurgeNowAsync();

        result.PurgedRunIds.Should().Equal(run.RunId);
        messages.Should().Contain(message => message.Contains("could not be saved"));
    }

    [Fact]
    public async Task HistoryPurge_keeps_torn_down_facility_when_snapshot_delete_fails()
    {
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var guidFacility = Guid.NewGuid().ToString();
        var guidRun = Run(guidFacility, now.AddDays(-30));
        var saved = new List<CleanupReport>();
        var service = Create(
            now,
            [guidRun],
            saved,
            deleteRunError: new InvalidOperationException("snapshot delete failed"));

        var result = await service.RunHistoryPurgeNowAsync();

        result.TornDownFacilityIds.Should().BeEquivalentTo([guidFacility, guidRun.RunId.ToString()]);
        result.PurgedRunIds.Should().BeEmpty();
        result.FailedRunIds.Should().Equal(guidRun.RunId);
        saved.Should().ContainSingle();
        saved[0].TornDownFacilityIds.Should().BeEquivalentTo([guidFacility, guidRun.RunId.ToString()]);
        saved[0].FailedRunIds.Should().Equal(guidRun.RunId);
    }

    [Fact]
    public async Task HistoryPurge_cancel_keeps_facilities_already_torn_down()
    {
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var firstFacility = Guid.NewGuid().ToString();
        var secondFacility = Guid.NewGuid().ToString();
        var first = Run(firstFacility, now.AddDays(-30));
        var second = Run(secondFacility, now.AddDays(-20));
        var saved = new List<CleanupReport>();
        using var cts = new CancellationTokenSource();
        var service = Create(now, [first, second], saved, cts);

        var act = () => service.RunHistoryPurgeNowAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        saved.Should().ContainSingle();
        saved[0].Status.Should().Be("failed");
        saved[0].TornDownFacilityIds.Should().BeEquivalentTo([firstFacility, first.RunId.ToString()]);
        saved[0].PurgedRunIds.Should().Equal(first.RunId);
        saved[0].Message.Should().Contain("cancelled");
    }

    private static AutomationRunSummary Run(string facilityId, DateTimeOffset finishedAt)
        => new()
        {
            RunId = Guid.NewGuid(),
            FacilityId = facilityId,
            AutomationCreatedFacility = true,
            ReportId = Guid.NewGuid().ToString(),
            Status = AutomationRunStatus.Succeeded,
            FinishedAt = finishedAt
        };

    private static LeftoverRunCleanupService Create(
        DateTimeOffset now,
        IReadOnlyList<AutomationRunSummary> runs,
        List<CleanupReport> saved,
        CancellationTokenSource? cancelAfterFirstDelete = null,
        List<string>? order = null,
        Exception? deleteRunError = null,
        IReadOnlyDictionary<string, string>? facilities = null,
        bool throwOnTerminalPublish = false,
        bool failFacilityDelete = false,
        int failFirstFacilityDeletes = 0,
        bool failReportSave = false,
        List<string>? terminalMessages = null,
        List<string>? statusesWhenSettingsLoad = null,
        List<string>? deletedFacilityIds = null,
        IReadOnlyList<string>? retainedFacilityIds = null,
        DateTimeOffset? retainedEligibleAt = null,
        List<string>? releasedFacilityIds = null,
        int maxFacilitiesPerPass = 25)
    {
        var facility = new Mock<IFacilityServiceClient>();
        facility.Setup(c => c.GetFacilityListAsync(It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<Dictionary<string, string>>
            {
                StatusCode = 200,
                Body = facilities?.ToDictionary(pair => pair.Key, pair => pair.Value)
                    ?? runs.ToDictionary(run => run.FacilityId!, run => run.FacilityId!)
            });
        facility.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<FacilityModel> { StatusCode = 404 });
        var remainingDeleteFailures = failFacilityDelete ? int.MaxValue : failFirstFacilityDeletes;
        facility.Setup(c => c.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                if (remainingDeleteFailures <= 0)
                    return Task.FromResult(Ok());

                if (remainingDeleteFailures < int.MaxValue)
                    remainingDeleteFailures--;
                throw new InvalidOperationException("facility delete failed");
            });
        if (deletedFacilityIds != null)
        {
            facility.Setup(c => c.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string facilityId, CancellationToken _) =>
                {
                    deletedFacilityIds.Add(facilityId);
                    return Task.FromResult(Ok());
                });
        }

        var normalization = new Mock<INormalizationServiceClient>();
        normalization.Setup(c => c.DeleteFacilityOperationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok());

        var dataAcq = new Mock<IDataAcquisitionServiceClient>();
        dataAcq.Setup(c => c.CancelAcquisitionLogsByFilterAsync(It.IsAny<object>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<DataAcquisitionBulkActionResultApiModel> { StatusCode = 200 });
        dataAcq.Setup(c => c.SoftDeleteLogsByFacilityAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok());
        dataAcq.Setup(c => c.DeleteQueryPlanAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok());
        dataAcq.Setup(c => c.DeleteFhirListConfigurationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok());
        dataAcq.Setup(c => c.DeleteFhirQueryConfigurationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok());

        var queryDispatch = new Mock<IQueryDispatchServiceClient>();
        queryDispatch.Setup(c => c.DeleteQueryDispatchConfigurationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok());

        var census = new Mock<ICensusServiceClient>();
        census.Setup(c => c.DisableFacilityJobsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok());
        census.Setup(c => c.DeleteCensusConfigAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok());

        var reportClient = new Mock<IReportServiceClient>();
        reportClient.Setup(c => c.SetReportsDeletedStatusForFacilityAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok());
        reportClient.Setup(c => c.SoftDeleteScheduleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(Ok());

        var services = new ServiceCollection();
        services.AddSingleton(facility.Object);
        services.AddSingleton(normalization.Object);
        services.AddSingleton(dataAcq.Object);
        services.AddSingleton(queryDispatch.Object);
        services.AddSingleton(census.Object);
        services.AddSingleton(reportClient.Object);
        var provider = services.BuildServiceProvider();
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(s => s.ServiceProvider).Returns(provider);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        var snapshots = new Mock<ISnapshotStore>();
        snapshots.Setup(s => s.GetAllRunSummariesAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);
        snapshots.Setup(s => s.GetRetainedFacilitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((retainedFacilityIds ?? [])
                .Select(id => new RetainedFacility(id, retainedEligibleAt ?? new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero)))
                .ToList());
        snapshots.Setup(s => s.ReleaseRetainedFacilityAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((id, _) => releasedFacilityIds?.Add(id))
            .Returns(Task.CompletedTask);
        snapshots.Setup(s => s.DeleteRunAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                cancelAfterFirstDelete?.Cancel();
                if (deleteRunError != null)
                    throw deleteRunError;
            })
            .Returns(Task.CompletedTask);

        LeftoverRunCleanupService? built = null;
        var settings = new Mock<ICleanupSettingsStore>();
        settings.Setup(s => s.GetEffectiveAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                if (built != null && statusesWhenSettingsLoad != null)
                    statusesWhenSettingsLoad.Add(built.CurrentActivity.Status);
                return Task.FromResult(new LeftoverRunCleanupSettings
                {
                    TeardownRetention = TimeSpan.FromDays(14),
                    MaxFacilitiesPerPass = maxFacilitiesPerPass
                });
            });

        var reports = new Mock<ICleanupReportStore>();
        reports.Setup(s => s.SaveAsync(It.IsAny<CleanupReport>(), It.IsAny<CancellationToken>()))
            .Callback<CleanupReport, CancellationToken>((report, _) =>
            {
                if (failReportSave)
                    throw new InvalidOperationException("report store unavailable");
                order?.Add("save");
                saved.Add(report);
            })
            .Returns(Task.CompletedTask);

        var abort = new Mock<IPipelineAbortRegistry>();
        abort.Setup(a => a.AbortAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var proxy = new Mock<IClientProxy>();
        proxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((_, args, _) =>
            {
                if (args.Length > 0 && args[0] is CleanupActivity activity && activity.Status is "completed" or "failed")
                {
                    order?.Add("publish");
                    if (throwOnTerminalPublish)
                        throw new OperationCanceledException();
                    terminalMessages?.Add(activity.Message);
                }
            })
            .Returns(Task.CompletedTask);
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);
        var hub = new Mock<IHubContext<CleanupHub>>();
        hub.SetupGet(h => h.Clients).Returns(clients.Object);

        built = new LeftoverRunCleanupService(
            scopeFactory.Object,
            snapshots.Object,
            new FakeTimeProvider(now),
            settings.Object,
            reports.Object,
            abort.Object,
            hub.Object,
            Options.Create(new LeftoverRunCleanupOptions()),
            NullLogger<LeftoverRunCleanupService>.Instance);
        return built;
    }

    private static LinkApiResponse Ok() => new() { StatusCode = 200 };
}
