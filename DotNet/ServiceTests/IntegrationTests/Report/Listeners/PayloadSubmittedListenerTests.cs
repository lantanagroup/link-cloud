using Confluent.Kafka;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Listeners;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report.Listeners;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class PayloadSubmittedListenerTests
{
    private readonly ReportIntegrationTestFixture _fixture;

    public PayloadSubmittedListenerTests(ReportIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ProcessMessageAsync_NullResult_ThrowsNullReferenceException()
    {
        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<PayloadSubmittedListener>();

        var consumeResult = (ConsumeResult<PayloadSubmittedKey, PayloadSubmittedValue>)null!;

        await Assert.ThrowsAsync<NullReferenceException>(
            () => listener.ProcessMessageAsync(consumeResult, CancellationToken.None));
    }

    [Fact]
    public async Task ProcessMessageAsync_MissingCorrelationId_ThrowsDeadLetterException()
    {
        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<PayloadSubmittedListener>();

        var facilityId = "test-facility-payload";
        var reportId = Guid.NewGuid();

        var key = new PayloadSubmittedKey { FacilityId = facilityId, ReportScheduleId = reportId };
        var value = new PayloadSubmittedValue { PayloadType = PayloadType.ReportSchedule };

        var consumeResult = new ConsumeResult<PayloadSubmittedKey, PayloadSubmittedValue>
        {
            Message = new Message<PayloadSubmittedKey, PayloadSubmittedValue>
            {
                Key = key,
                Value = value,
                Headers = new Headers()
            }
        };

        var exception = await Assert.ThrowsAsync<DeadLetterException>(
            () => listener.ProcessMessageAsync(consumeResult, CancellationToken.None));

        Assert.Contains("without correlation ID", exception.Message);
    }

    [Fact]
    public async Task ProcessMessageAsync_ReportSchedule_UpdatesScheduleStatus()
    {
        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<PayloadSubmittedListener>();
        var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var facilityId = "test-facility-schedule";
        var reportId = Guid.NewGuid();

        var schedule = new ReportScheduleModel
        {
            Id = reportId,
            FacilityId = facilityId,
            ReportStartDate = DateTime.UtcNow.AddDays(-30),
            ReportEndDate = DateTime.UtcNow.AddDays(30),
            Frequency = Frequency.Monthly,
            ReportTypes = { "DE-111" },
            Status = ScheduleStatus.Scheduled,
            CreateDate = DateTime.UtcNow
        };
        await reportScheduledManager.AddAsync(schedule, CancellationToken.None);

        var key = new PayloadSubmittedKey { FacilityId = facilityId, ReportScheduleId = reportId };
        var value = new PayloadSubmittedValue { PayloadType = PayloadType.ReportSchedule };

        var headers = new Headers { { "X-Correlation-Id", Encoding.UTF8.GetBytes("corr-123") } };

        var consumeResult = new ConsumeResult<PayloadSubmittedKey, PayloadSubmittedValue>
        {
            Message = new Message<PayloadSubmittedKey, PayloadSubmittedValue>
            {
                Key = key,
                Value = value,
                Headers = headers
            }
        };

        await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

        using var verifyScope = _fixture.ScopeFactory.CreateScope();
        var verifyManager = verifyScope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
        var updated = await verifyManager.SingleOrDefaultAsync(x => x.Id == reportId);

        Assert.Equal(ScheduleStatus.Submitted, updated.Status);
        Assert.NotNull(updated.SubmitReportDateTime);
    }

    [Fact]
    public async Task ProcessMessageAsync_MeasureReportSubmissionEntry_UpdatesEntryAndProducesManifest()
    {
        _fixture.SubmitPayloadKafkaProducerMock.Reset();
        _fixture.TenantApiServiceMock.Setup(x => x.GetFacilityConfig(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FacilityModel { FacilityName = "Test Facility" });

        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<PayloadSubmittedListener>();
        var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
        var reportEntryManager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var facilityId = "test-facility-entry";
        var reportId = Guid.NewGuid();
        var patientId = "pat-001";

        var schedule = new ReportScheduleModel
        {
            Id = reportId,
            FacilityId = facilityId,
            ReportStartDate = DateTime.UtcNow.AddDays(-30),
            ReportEndDate = DateTime.UtcNow.AddDays(30),
            Frequency = Frequency.Monthly,
            ReportTypes = { "DE-111" },
            Status = ScheduleStatus.Scheduled,
            CreateDate = DateTime.UtcNow,
            EndOfReportPeriodJobHasRun = true
        };
        await reportScheduledManager.AddAsync(schedule, CancellationToken.None);

        var entry = new ReportEntryModel
        {
            PatientId = patientId,
            ReportScheduleId = reportId,
            FacilityId = facilityId,
            ReportingStatus = ReportingStatus.PassedValidation,
            SubmissionStatus = SubmissionStatus.PendingValidation,
            CreateDate = DateTime.UtcNow
        };
        await reportEntryManager.AddAsync(entry, CancellationToken.None);

        var key = new PayloadSubmittedKey { FacilityId = facilityId, ReportScheduleId = reportId };
        var value = new PayloadSubmittedValue
        {
            PayloadType = PayloadType.MeasureReportSubmissionEntry,
            PatientId = patientId
        };

        var headers = new Headers { { "X-Correlation-Id", Encoding.UTF8.GetBytes("corr-456") } };

        var consumeResult = new ConsumeResult<PayloadSubmittedKey, PayloadSubmittedValue>
        {
            Message = new Message<PayloadSubmittedKey, PayloadSubmittedValue>
            {
                Key = key,
                Value = value,
                Headers = headers
            }
        };

        await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

        using var verifyScope = _fixture.ScopeFactory.CreateScope();
        var verifyEntryManager = verifyScope.ServiceProvider.GetRequiredService<IReportEntryManager>();
        var updatedEntry = await verifyEntryManager.SingleOrDefaultAsync(e => e.PatientId == patientId && e.ReportScheduleId == reportId);

        Assert.Equal(SubmissionStatus.Submitted, updatedEntry.SubmissionStatus);
        Assert.NotNull(updatedEntry.SubmitReportDateTime);

        _fixture.SubmitPayloadKafkaProducerMock.Verify(
            p => p.Produce(
                It.IsAny<string>(),
                It.Is<Message<SubmitPayloadKey, SubmitPayloadValue>>(m => m.Value.PayloadType == PayloadType.ReportSchedule),
                It.IsAny<Action<DeliveryReport<SubmitPayloadKey, SubmitPayloadValue>>>()),
            Times.Once);
    }
}
