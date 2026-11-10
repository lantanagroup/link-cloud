using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.DataAcquisition.Jobs;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Quartz;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class TailMessageRecoveryJobTests
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public TailMessageRecoveryJobTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Execute_RespectsMaxGroupsPerRun_ProcessesSmallBatchOnly()
    {
        _fixture.ResourcesAcquiredProducerMock.Reset();

        var tag = Guid.NewGuid().ToString("N");
        var facilityId = $"TailRecoveryFac_{tag}";
        var correlationA = $"CorrA_{tag}";
        var correlationB = $"CorrB_{tag}";

        using (var scope = _fixture.ServiceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

            // Isolate this test from shared fixture state: exclude pre-existing
            // orphaned groups from safety-net selection so MaxGroupsPerRun targets
            // only groups seeded by this test.
            await db.DataAcquisitionLogs
                .Where(l => !l.TailSent)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(l => l.TailSent, true)
                    .SetProperty(l => l.ModifyDate, DateTime.UtcNow));

            // Group A (orphaned, fully terminal)
            await SeedTailGroupAsync(db, facilityId, correlationA, siblingCount: 2);

            // Group B (also orphaned, fully terminal)
            await SeedTailGroupAsync(db, facilityId, correlationB, siblingCount: 2);
        }

        var settings = Options.Create(new TailMessageRecoveryJobSettings
        {
            MinAgeMinutes = 0,
            MaxGroupsPerRun = 1,
            TimeBudgetPerRunSeconds = 30
        });

        var logger = new Mock<ILogger<TailMessageRecoveryJob>>().Object;
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var producer = _fixture.ServiceProvider.GetRequiredService<IProducer<ResourceKey, ResourcesAcquired>>();

        var job = new TailMessageRecoveryJob(logger, scopeFactory, producer, settings);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        await job.Execute(jobContextMock.Object);

        _fixture.ResourcesAcquiredProducerMock.Verify(
            p => p.ProduceAsync(
                KafkaTopic.ResourcesAcquired.ToString(),
                It.IsAny<Message<ResourceKey, ResourcesAcquired>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var groupATailFlags = await assertDb.DataAcquisitionLogs
            .Where(l => l.FacilityId == facilityId && l.CorrelationId == correlationA)
            .Select(l => l.TailSent)
            .ToListAsync();

        var groupBTailFlags = await assertDb.DataAcquisitionLogs
            .Where(l => l.FacilityId == facilityId && l.CorrelationId == correlationB)
            .Select(l => l.TailSent)
            .ToListAsync();

        var groupsMarkedComplete = 0;
        if (groupATailFlags.Count > 0 && groupATailFlags.All(x => x)) groupsMarkedComplete++;
        if (groupBTailFlags.Count > 0 && groupBTailFlags.All(x => x)) groupsMarkedComplete++;

        Assert.Equal(1, groupsMarkedComplete);
    }

    [Fact]
    public async Task Execute_WhenTimeBudgetIsZero_DoesNotProcessAnyGroups()
    {
        _fixture.ResourcesAcquiredProducerMock.Reset();

        var tag = Guid.NewGuid().ToString("N");
        var facilityId = $"TailRecoveryFac_{tag}";
        var correlation = $"Corr_{tag}";

        using (var scope = _fixture.ServiceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

            // Isolate this test from shared fixture state.
            await db.DataAcquisitionLogs
                .Where(l => !l.TailSent)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(l => l.TailSent, true)
                    .SetProperty(l => l.ModifyDate, DateTime.UtcNow));

            await SeedTailGroupAsync(db, facilityId, correlation, siblingCount: 2);
        }

        var settings = Options.Create(new TailMessageRecoveryJobSettings
        {
            MinAgeMinutes = 0,
            MaxGroupsPerRun = 10,
            TimeBudgetPerRunSeconds = 0
        });

        var logger = new Mock<ILogger<TailMessageRecoveryJob>>().Object;
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var producer = _fixture.ServiceProvider.GetRequiredService<IProducer<ResourceKey, ResourcesAcquired>>();

        var job = new TailMessageRecoveryJob(logger, scopeFactory, producer, settings);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        await job.Execute(jobContextMock.Object);

        _fixture.ResourcesAcquiredProducerMock.Verify(
            p => p.ProduceAsync(
                KafkaTopic.ResourcesAcquired.ToString(),
                It.IsAny<Message<ResourceKey, ResourcesAcquired>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var stillUnclaimed = await assertDb.DataAcquisitionLogs
            .Where(l => l.FacilityId == facilityId && l.CorrelationId == correlation)
            .AllAsync(l => !l.TailSent);

        Assert.True(stillUnclaimed);
    }

    [Fact]
    public async Task Execute_ReclaimsStaleTailClaim_ProducesAndMarksSent()
    {
        _fixture.ResourcesAcquiredProducerMock.Reset();

        var tag = Guid.NewGuid().ToString("N");
        var facilityId = $"TailRecoveryFac_{tag}";
        var correlation = $"Corr_{tag}";
        var staleClaim = DateTime.UtcNow.Subtract(DataAcquisitionLog.TailClaimLease).AddMinutes(-1);

        using (var scope = _fixture.ServiceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

            await db.DataAcquisitionLogs
                .Where(l => !l.TailSent)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(l => l.TailSent, true)
                    .SetProperty(l => l.ModifyDate, DateTime.UtcNow));

            await SeedTailGroupAsync(db, facilityId, correlation, siblingCount: 2, tailClaimedAt: staleClaim);
        }

        var settings = Options.Create(new TailMessageRecoveryJobSettings
        {
            MinAgeMinutes = 0,
            MaxGroupsPerRun = 10,
            TimeBudgetPerRunSeconds = 30
        });

        var logger = new Mock<ILogger<TailMessageRecoveryJob>>().Object;
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var producer = _fixture.ServiceProvider.GetRequiredService<IProducer<ResourceKey, ResourcesAcquired>>();

        var job = new TailMessageRecoveryJob(logger, scopeFactory, producer, settings);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        await job.Execute(jobContextMock.Object);

        _fixture.ResourcesAcquiredProducerMock.Verify(
            p => p.ProduceAsync(
                KafkaTopic.ResourcesAcquired.ToString(),
                It.IsAny<Message<ResourceKey, ResourcesAcquired>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var sent = await assertDb.DataAcquisitionLogs
            .Where(l => l.FacilityId == facilityId && l.CorrelationId == correlation)
            .AllAsync(l => l.TailSent);

        Assert.True(sent);
    }

    [Fact]
    public async Task Execute_SkipsInFlightTailClaim_DoesNotProduce()
    {
        _fixture.ResourcesAcquiredProducerMock.Reset();

        var tag = Guid.NewGuid().ToString("N");
        var facilityId = $"TailRecoveryFac_{tag}";
        var correlation = $"Corr_{tag}";

        using (var scope = _fixture.ServiceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

            await db.DataAcquisitionLogs
                .Where(l => !l.TailSent)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(l => l.TailSent, true)
                    .SetProperty(l => l.ModifyDate, DateTime.UtcNow));

            await SeedTailGroupAsync(db, facilityId, correlation, siblingCount: 2, tailClaimedAt: DateTime.UtcNow);
        }

        var settings = Options.Create(new TailMessageRecoveryJobSettings
        {
            MinAgeMinutes = 0,
            MaxGroupsPerRun = 10,
            TimeBudgetPerRunSeconds = 30
        });

        var logger = new Mock<ILogger<TailMessageRecoveryJob>>().Object;
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var producer = _fixture.ServiceProvider.GetRequiredService<IProducer<ResourceKey, ResourcesAcquired>>();

        var job = new TailMessageRecoveryJob(logger, scopeFactory, producer, settings);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        await job.Execute(jobContextMock.Object);

        _fixture.ResourcesAcquiredProducerMock.Verify(
            p => p.ProduceAsync(
                KafkaTopic.ResourcesAcquired.ToString(),
                It.IsAny<Message<ResourceKey, ResourcesAcquired>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var stillUnsent = await assertDb.DataAcquisitionLogs
            .Where(l => l.FacilityId == facilityId && l.CorrelationId == correlation)
            .AllAsync(l => !l.TailSent);

        Assert.True(stillUnsent);
    }

    [Theory]
    [InlineData(RequestStatus.Completed, true, false)]
    [InlineData(RequestStatus.MaxRetriesReached, false, false)]
    [InlineData(RequestStatus.ConfigurationMissing, false, false)]
    [InlineData(RequestStatus.Cancelled, false, false)]
    [InlineData(RequestStatus.AccessDenied, false, false)]
    [InlineData(RequestStatus.Completed, true, true)]
    [InlineData(RequestStatus.ConfigurationMissing, false, true)]
    public async Task Execute_ReportsAcquisitionOutcomeAndOnlyHandsOffSuccessfulGroups(RequestStatus status, bool success, bool failPublication)
    {
        _fixture.ResourcesAcquiredProducerMock.Reset();
        _fixture.StatusProducerMock.Reset();
        if (failPublication)
            _fixture.StatusProducerMock.Setup(p => p.ProduceAsync(It.IsAny<string>(),
                    It.IsAny<Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("broker unavailable"));
        var facility = $"Status_{Guid.NewGuid():N}";
        var correlation = Guid.NewGuid().ToString();
        var reportId = Guid.NewGuid();
        long failedLogId;
        using (var scope = _fixture.ServiceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
            await db.DataAcquisitionLogs.Where(l => !l.TailSent)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.TailSent, true));
            db.ScheduledReports.Add(new ScheduledReportEntity
            {
                ReportTrackingId = reportId, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            await SeedTailGroupAsync(db, facility, correlation, 2);
            var logs = await db.DataAcquisitionLogs.Where(l => l.CorrelationId == correlation).OrderBy(l => l.Id).ToListAsync();
            logs[0].Status = status;
            failedLogId = logs[0].Id;
            foreach (var log in logs) log.ReportTrackingId = reportId;
            await db.SaveChangesAsync();
        }
        var job = new TailMessageRecoveryJob(Mock.Of<ILogger<TailMessageRecoveryJob>>(),
            _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            _fixture.ResourcesAcquiredProducerMock.Object,
            Options.Create(new TailMessageRecoveryJobSettings { MinAgeMinutes = 0, MaxGroupsPerRun = 10, TimeBudgetPerRunSeconds = 30 }));
        var context = new Mock<IJobExecutionContext>();
        await job.Execute(context.Object);

        _fixture.StatusProducerMock.Verify(p => p.ProduceAsync("ReportStatusUpdated",
            It.Is<Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>>(m =>
                m.Key.ReportId == reportId.ToString() && m.Value.IsSuccess == success
                && m.Value.Status == LantanaGroup.Link.Shared.Application.Enums.Reporting.PatientReportingStatus.InitialAcquisition
                && (success || m.Value.ErrorMessage!.Contains(failedLogId.ToString()))),
            It.IsAny<CancellationToken>()), Times.Once);
        _fixture.ResourcesAcquiredProducerMock.Verify(p => p.ProduceAsync(It.IsAny<string>(),
            It.IsAny<Message<ResourceKey, ResourcesAcquired>>(), It.IsAny<CancellationToken>()),
            success && !failPublication ? Times.Once() : Times.Never());
        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        Assert.True(await assertDb.DataAcquisitionLogs.Where(l => l.CorrelationId == correlation)
            .AllAsync(l => l.TailSent == !failPublication && (failPublication ? l.TailClaimedAt == null : l.TailClaimedAt != null)));
    }

    private static async Task SeedTailGroupAsync(
        DataAcquisitionDbContext db,
        string facilityId,
        string correlationId,
        int siblingCount,
        DateTime? tailClaimedAt = null)
    {
        var now = DateTime.UtcNow;
        var modifyDate = tailClaimedAt ?? now.AddMinutes(-2);

        db.DataAcquisitionLogs.AddRange(
            new DataAcquisitionLog
            {
                FacilityId = facilityId,
                CorrelationId = correlationId,
                QueryPhase = QueryPhase.Initial,
                Status = RequestStatus.Completed,
                TailSent = false,
                TailClaimedAt = tailClaimedAt,
                SiblingCount = siblingCount,
                PatientId = "Patient/1",
                CreateDate = now.AddMinutes(-5),
                ModifyDate = modifyDate
            },
            new DataAcquisitionLog
            {
                FacilityId = facilityId,
                CorrelationId = correlationId,
                QueryPhase = QueryPhase.Initial,
                Status = RequestStatus.Completed,
                TailSent = false,
                TailClaimedAt = tailClaimedAt,
                SiblingCount = siblingCount,
                PatientId = "Patient/1",
                CreateDate = now.AddMinutes(-5),
                ModifyDate = modifyDate
            }
        );

        await db.SaveChangesAsync();
    }
}

