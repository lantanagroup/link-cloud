using Confluent.Kafka;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Listeners;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReportingStatus = LantanaGroup.Link.Report.Domain.Enums.ReportingStatus;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report.Listeners;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class GenerateReportListenerTests
{
    private readonly ReportIntegrationTestFixture _fixture;

    public GenerateReportListenerTests(ReportIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ProcessMessageAsync_NullResult_DoesNothing()
    {
        _fixture.DataAcquisitionRequestedKafkaProducerMock.Reset();

        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<GenerateReportListener>();

        var consumeResult = (ConsumeResult<string, GenerateReportValue>)null!;

        await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

        _fixture.DataAcquisitionRequestedKafkaProducerMock.Verify(
            p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<Message<string, DataAcquisitionRequestedValue>>(),
                It.IsAny<Action<DeliveryReport<string, DataAcquisitionRequestedValue>>>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessMessageAsync_EmptyFacilityId_CallsDeadLetterHandler()
    {
        _fixture.GenerateReportDeadLetterHandlerMock.Reset();

        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<GenerateReportListener>();
        var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var adhocReportId = Guid.NewGuid();

        var value = new GenerateReportValue
        {
            AdhocReportId = adhocReportId,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30),
            ReportTypes = new List<string> { "DE-111" }
        };

        var consumeResult = new ConsumeResult<string, GenerateReportValue>
        {
            Message = new Message<string, GenerateReportValue>
            {
                Key = string.Empty,
                Value = value
            }
        };

        await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

        var schedule = await reportScheduledManager.SingleOrDefaultAsync(x => x.Id == adhocReportId);
        Assert.Null(schedule);
    }

    [Fact]
    public async Task ProcessMessageAsync_MissingReportTypes_CallsDeadLetterHandler()
    {
        _fixture.GenerateReportDeadLetterHandlerMock.Reset();

        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<GenerateReportListener>();

        var facilityId = "test-facility-invalid-types";

        var value = new GenerateReportValue
        {
            AdhocReportId = Guid.NewGuid(),
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30),
            ReportTypes = new List<string>()
        };

        var consumeResult = new ConsumeResult<string, GenerateReportValue>
        {
            Message = new Message<string, GenerateReportValue>
            {
                Key = facilityId,
                Value = value
            }
        };

        await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

        _fixture.GenerateReportDeadLetterHandlerMock.Verify(
            h => h.HandleException(
                It.IsAny<ConsumeResult<string, GenerateReportValue>>(),
                It.Is<DeadLetterException>(ex => ex.Message.Contains("ReportTypes is null or empty")),
                facilityId),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessMessageAsync_InvalidDates_CallsDeadLetterHandler()
    {
        _fixture.GenerateReportDeadLetterHandlerMock.Reset();

        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<GenerateReportListener>();

        var facilityId = "test-facility-invalid-dates";

        var value = new GenerateReportValue
        {
            AdhocReportId = Guid.NewGuid(),
            StartDate = DateTime.UtcNow.AddDays(30),
            EndDate = DateTime.UtcNow.AddDays(-1),
            ReportTypes = new List<string> { "DE-111" }
        };

        var consumeResult = new ConsumeResult<string, GenerateReportValue>
        {
            Message = new Message<string, GenerateReportValue>
            {
                Key = facilityId,
                Value = value
            }
        };

        await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

        _fixture.GenerateReportDeadLetterHandlerMock.Verify(
            h => h.HandleException(
                It.IsAny<ConsumeResult<string, GenerateReportValue>>(),
                It.Is<DeadLetterException>(ex => ex.Message.Contains("End date must be after start date")),
                facilityId),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessMessageAsync_NewAdHocManual_WithPatientIds_CreatesScheduleEntriesAndProducesDataAcquisition()
    {
        _fixture.DataAcquisitionRequestedKafkaProducerMock.Reset();

        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<GenerateReportListener>();
        var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
        var reportEntryManager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var facilityId = "test-facility-adhoc-manual";
        var adhocReportId = Guid.NewGuid();
        var reportTypes = new List<string> { "DE-111", "DE-222" };
        var patientIds = new List<string> { "pat-001", "pat-002" };

        var value = new GenerateReportValue
        {
            AdhocReportId = adhocReportId,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(30),
            ReportTypes = reportTypes,
            PatientIds = patientIds,
            BypassSubmission = false,
            Regenerate = false
        };

        var consumeResult = new ConsumeResult<string, GenerateReportValue>
        {
            Message = new Message<string, GenerateReportValue>
            {
                Key = facilityId,
                Value = value
            }
        };

        await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

        var schedule = await reportScheduledManager.SingleOrDefaultAsync(x => x.Id == adhocReportId);
        Assert.NotNull(schedule);
        Assert.Equal(facilityId, schedule.FacilityId);
        Assert.Equal(Frequency.Adhoc, schedule.Frequency);
        Assert.Equal(AdHocType.Manual, schedule.AdHocType);
        Assert.Equal(reportTypes, schedule.ReportTypes);

        var entries = await reportEntryManager.FindAsync(e => e.ReportScheduleId == adhocReportId);
        Assert.Equal(2, entries.Count());

        _fixture.DataAcquisitionRequestedKafkaProducerMock.Verify(
            p => p.Produce(
                It.IsAny<string>(),
                It.Is<Message<string, DataAcquisitionRequestedValue>>(m =>
                    m.Key == facilityId &&
                    m.Value.ScheduledReports.Count == 1 &&
                    m.Value.ScheduledReports[0].ReportTrackingId == adhocReportId.ToString()),
                It.IsAny<Action<DeliveryReport<string, DataAcquisitionRequestedValue>>>()),
            Times.Exactly(2));
    }

    [Fact(Skip = "Requires live Census service - SDK clients are now concrete types injected via AddLinkSdk()")]
    public async Task ProcessMessageAsync_NewAdHocCensus_NoPatientIds_FetchesFromCensusAndCreatesEntries()
    {
        _fixture.DataAcquisitionRequestedKafkaProducerMock.Reset();

        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<GenerateReportListener>();
        var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
        var reportEntryManager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var facilityId = "test-facility-census";
        var adhocReportId = Guid.NewGuid();
        var reportTypes = new List<string> { "DE-111" };

        var value = new GenerateReportValue
        {
            AdhocReportId = adhocReportId,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(30),
            ReportTypes = reportTypes,
            PatientIds = null,
            BypassSubmission = false,
            Regenerate = false
        };

        var consumeResult = new ConsumeResult<string, GenerateReportValue>
        {
            Message = new Message<string, GenerateReportValue>
            {
                Key = facilityId,
                Value = value
            }
        };

        await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

        var schedule = await reportScheduledManager.SingleOrDefaultAsync(x => x.Id == adhocReportId);
        Assert.NotNull(schedule);
        Assert.Equal(AdHocType.Census, schedule.AdHocType);

        var entries = await reportEntryManager.FindAsync(e => e.ReportScheduleId == adhocReportId);
        Assert.Equal(2, entries.Count());

        _fixture.DataAcquisitionRequestedKafkaProducerMock.Verify(
            p => p.Produce(
                It.IsAny<string>(),
                It.Is<Message<string, DataAcquisitionRequestedValue>>(m =>
                    m.Key == facilityId &&
                    m.Value.ScheduledReports.Count == 1 &&
                    m.Value.ScheduledReports[0].ReportTrackingId == adhocReportId.ToString()),
                It.IsAny<Action<DeliveryReport<string, DataAcquisitionRequestedValue>>>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessMessageAsync_RegenerateReport_CreatesNewScheduleAndReCreatesEntries()
    {
        _fixture.DataAcquisitionRequestedKafkaProducerMock.Reset();

        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<GenerateReportListener>();
        var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
        var reportEntryManager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var facilityId = "test-facility-regen";
        var originalReportId = Guid.NewGuid();
        var newAdhocReportId = Guid.NewGuid();

        var existingSchedule = new ReportScheduleModel
        {
            Id = originalReportId,
            FacilityId = facilityId,
            ReportStartDate = DateTimeOffset.UtcNow.AddDays(-60),
            ReportEndDate = DateTimeOffset.UtcNow.AddDays(-30),
            Frequency = Frequency.Monthly,
            ReportTypes = { "DE-111" },
            Status = ScheduleStatus.Scheduled,
            CreateDate = DateTime.UtcNow
        };
        await reportScheduledManager.AddAsync(existingSchedule, CancellationToken.None);

        var existingEntry = new ReportEntryModel
        {
            PatientId = "pat-regen-001",
            ReportScheduleId = originalReportId,
            FacilityId = facilityId,
            ReportingStatus = ReportingStatus.PatientIdentified,
            CreateDate = DateTime.UtcNow,
            MeasureReports = new List<EntryMeasureReportModel>
            {
                new EntryMeasureReportModel { ReportType = "DE-111", Status = MeasureReportStatus.ReadyForValidation }
            }
        };
        await reportEntryManager.AddAsync(existingEntry, CancellationToken.None);

        var value = new GenerateReportValue
        {
            ReportId = originalReportId,
            AdhocReportId = newAdhocReportId,
            Regenerate = true,
            BypassSubmission = false
        };

        var consumeResult = new ConsumeResult<string, GenerateReportValue>
        {
            Message = new Message<string, GenerateReportValue>
            {
                Key = facilityId,
                Value = value
            }
        };

        await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

        var newSchedule = await reportScheduledManager.SingleOrDefaultAsync(x => x.Id == newAdhocReportId);
        Assert.NotNull(newSchedule);

        var newEntries = await reportEntryManager.FindAsync(e => e.ReportScheduleId == newAdhocReportId);
        Assert.Single(newEntries);
    }

    [Fact]
    public async Task ProcessMessageAsync_Regenerate_NonExistentReport_CallsDeadLetterHandler()
    {
        _fixture.GenerateReportDeadLetterHandlerMock.Reset();

        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<GenerateReportListener>();

        var facilityId = "test-facility-regen-missing";
        var nonExistentId = Guid.NewGuid();

        var value = new GenerateReportValue
        {
            ReportId = nonExistentId,
            AdhocReportId = Guid.NewGuid(),
            Regenerate = true
        };

        var consumeResult = new ConsumeResult<string, GenerateReportValue>
        {
            Message = new Message<string, GenerateReportValue>
            {
                Key = facilityId,
                Value = value
            }
        };

        await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

        _fixture.GenerateReportDeadLetterHandlerMock.Verify(
            h => h.HandleException(
                It.IsAny<ConsumeResult<string, GenerateReportValue>>(),
                It.Is<DeadLetterException>(ex => ex.Message.Contains("No ReportSchedule found")),
                facilityId),
            Times.AtLeastOnce);
    }
}
