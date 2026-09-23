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
        result.TornDownFacilityIds.Should().Equal(guidFacility);
        result.TeardownCandidateCount.Should().Be(1);
        result.PurgedRunIds.Should().BeEquivalentTo([guidRun.RunId, namedRun.RunId]);
        saved.Should().ContainSingle();
        saved[0].TornDownFacilityIds.Should().Equal(guidFacility);
        saved[0].TeardownCandidateCount.Should().Be(1);
        saved[0].PurgedRunIds.Should().BeEquivalentTo([guidRun.RunId, namedRun.RunId]);
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
        saved[0].TornDownFacilityIds.Should().Equal(firstFacility);
        saved[0].PurgedRunIds.Should().Equal(first.RunId);
        saved[0].Message.Should().Contain("cancelled");
    }

    private static AutomationRunSummary Run(string facilityId, DateTimeOffset finishedAt)
        => new()
        {
            RunId = Guid.NewGuid(),
            FacilityId = facilityId,
            ReportId = Guid.NewGuid().ToString(),
            Status = AutomationRunStatus.Succeeded,
            FinishedAt = finishedAt
        };

    private static LeftoverRunCleanupService Create(
        DateTimeOffset now,
        IReadOnlyList<AutomationRunSummary> runs,
        List<CleanupReport> saved,
        CancellationTokenSource? cancelAfterFirstDelete = null,
        List<string>? order = null)
    {
        var facility = new Mock<IFacilityServiceClient>();
        facility.Setup(c => c.GetFacilityListAsync(It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<Dictionary<string, string>>
            {
                StatusCode = 200,
                Body = runs.ToDictionary(run => run.FacilityId!, run => run.FacilityId!)
            });
        facility.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<FacilityModel> { StatusCode = 404 });
        facility.Setup(c => c.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok());

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
        snapshots.Setup(s => s.DeleteRunAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback(() => cancelAfterFirstDelete?.Cancel())
            .Returns(Task.CompletedTask);

        var settings = new Mock<ICleanupSettingsStore>();
        settings.Setup(s => s.GetEffectiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeftoverRunCleanupSettings
            {
                TeardownRetention = TimeSpan.FromDays(14),
                MaxFacilitiesPerPass = 25
            });

        var reports = new Mock<ICleanupReportStore>();
        reports.Setup(s => s.SaveAsync(It.IsAny<CleanupReport>(), It.IsAny<CancellationToken>()))
            .Callback<CleanupReport, CancellationToken>((report, _) =>
            {
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
                    order?.Add("publish");
            })
            .Returns(Task.CompletedTask);
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);
        var hub = new Mock<IHubContext<CleanupHub>>();
        hub.SetupGet(h => h.Clients).Returns(clients.Object);

        return new LeftoverRunCleanupService(
            scopeFactory.Object,
            snapshots.Object,
            new FakeTimeProvider(now),
            settings.Object,
            reports.Object,
            abort.Object,
            hub.Object,
            Options.Create(new LeftoverRunCleanupOptions()),
            NullLogger<LeftoverRunCleanupService>.Instance);
    }

    private static LinkApiResponse Ok() => new() { StatusCode = 200 };
}
