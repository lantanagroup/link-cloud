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
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report
{
    [Collection("ReportIntegrationTests")]
    public class PatientEventListenerTests : IClassFixture<ReportIntegrationTestFixture>
    {
        private readonly ReportIntegrationTestFixture _fixture;

        public PatientEventListenerTests(ReportIntegrationTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ProcessMessageAsync_ValidPatientEvent_CreatesReportEntries()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var listener = scope.ServiceProvider.GetRequiredService<PatientEventListener>();
            var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
            var reportEntryManager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

            var facilityId = "test-facility-003";
            var reportId = Guid.NewGuid();

            var schedule = new ReportScheduleModel
            {
                Id = reportId,
                FacilityId = facilityId,
                ReportStartDate = DateTime.UtcNow.AddDays(-30),
                ReportEndDate = DateTime.UtcNow.AddDays(30),
                Frequency = Frequency.Monthly,
                ReportTypes = { "DE-111", "DE-222" }, 
                Status = ScheduleStatus.Scheduled,
                EndOfReportPeriodJobHasRun = false,
                CreateDate = DateTime.UtcNow
            };
            await reportScheduledManager.AddAsync(schedule, CancellationToken.None);

            var patientEventValue = new PatientEventValue 
            { 
                EventType = PatientEvents.Admit.ToString(),
                PatientId = "12345"
            };

            var consumeResult = new ConsumeResult<string, PatientEventValue>
            {
                Message = new Message<string, PatientEventValue>
                {
                    Key = facilityId,
                    Value = patientEventValue
                }
            };

            await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

            var entry1 = await reportEntryManager.SingleOrDefaultAsync(e => e.PatientId == "12345" && e.ReportScheduleId == reportId);

            Assert.NotNull(entry1);
            Assert.Equal(ReportingStatus.PatientIdentified, entry1.ReportingStatus);
            Assert.Equal(2, entry1.MeasureReports.Count);
        }

        [Fact]
        public async Task ProcessMessageAsync_DischargePatientEvent_Ignored()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var listener = scope.ServiceProvider.GetRequiredService<PatientEventListener>();
            var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
            var reportEntryManager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

            var facilityId = "test-facility-003";
            var reportId = Guid.NewGuid();

            var schedule = new ReportScheduleModel
            {
                Id = reportId,
                FacilityId = facilityId,
                ReportStartDate = DateTime.UtcNow.AddDays(-30),
                ReportEndDate = DateTime.UtcNow.AddDays(30),
                Frequency = Frequency.Monthly,
                ReportTypes = new List<string> { "DE-111", "DE-222" },
                Status = ScheduleStatus.Scheduled,
                EndOfReportPeriodJobHasRun = false,
                CreateDate = DateTime.UtcNow
            };
            await reportScheduledManager.AddAsync(schedule, CancellationToken.None);

            var patientEventValue = new PatientEventValue
            {
                EventType = PatientEvents.Discharge.ToString(),
                PatientId = "12345"
            };

            var consumeResult = new ConsumeResult<string, PatientEventValue>
            {
                Message = new Message<string, PatientEventValue>
                {
                    Key = facilityId,
                    Value = patientEventValue
                }
            };

            await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

            var entry1 = await reportEntryManager.SingleOrDefaultAsync(e => e.PatientId == "12345" && e.ReportScheduleId == reportId);

            Assert.Null(entry1);
        }

        [Fact]
        public async Task ProcessMessageAsync_ExistingReportEntry_AddsMissingMeasureReportsOnly()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var listener = scope.ServiceProvider.GetRequiredService<PatientEventListener>();
            var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
            var reportEntryManager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

            var facilityId = "test-facility-009";
            var reportId = Guid.NewGuid();

            var schedule = new ReportScheduleModel
            {
                Id = reportId,
                FacilityId = facilityId,
                ReportStartDate = DateTime.UtcNow.AddDays(-30),
                ReportEndDate = DateTime.UtcNow.AddDays(30),
                Frequency = Frequency.Monthly,
                ReportTypes = { "DE-111", "DE-333" },
                Status = ScheduleStatus.Scheduled,
                EndOfReportPeriodJobHasRun = false,
                CreateDate = DateTime.UtcNow
            };
            await reportScheduledManager.AddAsync(schedule, CancellationToken.None);

            var existingEntry = new ReportEntryModel
            {
                PatientId = "12345",
                ReportScheduleId = reportId,
                FacilityId = facilityId,
                ReportingStatus = ReportingStatus.PatientIdentified,
                MeasureReports = new List<EntryMeasureReportModel> { new EntryMeasureReportModel { MeasureReportId = Guid.NewGuid().ToString(), ReportType = "DE-111" } }
            };
            await reportEntryManager.AddAsync(existingEntry, CancellationToken.None);

            var consumeResult = new ConsumeResult<string, PatientEventValue>
            {
                Message = new Message<string, PatientEventValue>
                {
                    Key = facilityId,
                    Value = new PatientEventValue()
                    {
                        EventType = PatientEvents.Admit.ToString(),
                        PatientId = "12345"
                    }
                }
            };

            await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

            scope.Dispose();

            using var assertScope = _fixture.ScopeFactory.CreateScope();
            var assertReportEntryManager = assertScope.ServiceProvider.GetRequiredService<IReportEntryManager>();
            var updated = await assertReportEntryManager.SingleOrDefaultAsync(e => e.PatientId == "12345" && e.ReportScheduleId == reportId);
            Assert.Equal(2, updated.MeasureReports.Count);
        }

        [Fact]
        public async Task ProcessMessageAsync_NullResult_Ignored()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var listener = scope.ServiceProvider.GetRequiredService<PatientEventListener>();

            var consumeResult = (ConsumeResult<string, PatientEventValue>)null!;

            await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);
        }

        [Fact]
        public async Task ProcessMessageAsync_InvalidMessage_CallsTransientHandler()
        {
            _fixture.PatientEventDeadLetterHandlerMock.Reset();

            using var scope = _fixture.ScopeFactory.CreateScope();
            var listener = scope.ServiceProvider.GetRequiredService<PatientEventListener>();

            var consumeResult = new ConsumeResult<string, PatientEventValue>
            {
                Message = new Message<string, PatientEventValue>
                {
                    Key = "test-facility-",
                    Value = new PatientEventValue() {
                        EventType = PatientEvents.Admit.ToString(),
                        PatientId = "12345"
                    }
                }
            };

            await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

            _fixture.PatientEventTransientHandlerMock.Verify(
                h => h.HandleException(
                    It.IsAny<ConsumeResult<string, PatientEventValue>>(),
                    It.IsAny<TransientException>(),
                    "test-facility-"),
                Times.Once);
        }

        [Fact]
        public async Task ProcessMessageAsync_EmptyKey_CallsDeadLetterHandler()
        {
            _fixture.PatientEventDeadLetterHandlerMock.Reset();

            using var scope = _fixture.ScopeFactory.CreateScope();
            var listener = scope.ServiceProvider.GetRequiredService<PatientEventListener>();

            var consumeResult = new ConsumeResult<string, PatientEventValue>
            {
                Message = new Message<string, PatientEventValue>
                {
                    Key = string.Empty,
                    Value = new PatientEventValue() 
                    {
                        EventType = PatientEvents.Admit.ToString(),
                        PatientId = "12345"
                    }
                }
            };

            await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

            _fixture.PatientEventDeadLetterHandlerMock.Verify(h => h.HandleException(It.IsAny<ConsumeResult<string, PatientEventValue>>(), It.IsAny<DeadLetterException>(), string.Empty), Times.Once);
        }

        [Fact]
        public async Task ProcessMessageAsync_NoScheduledReports_CallsTransientHandler()
        {
            _fixture.PatientEventTransientHandlerMock.Reset();

            using var scope = _fixture.ScopeFactory.CreateScope();
            var listener = scope.ServiceProvider.GetRequiredService<PatientEventListener>();

            var facilityId = "test-facility-005";

            var consumeResult = new ConsumeResult<string, PatientEventValue>
            {
                Message = new Message<string, PatientEventValue>
                {
                    Key = facilityId,
                    Value = new PatientEventValue()
                    {
                        EventType = PatientEvents.Admit.ToString(),
                        PatientId = "12345"
                    }
                }
            };

            await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

            _fixture.PatientEventTransientHandlerMock.Verify(
                h => h.HandleException(
                    It.IsAny<ConsumeResult<string, PatientEventValue>>(),
                    It.IsAny<TransientException>(),
                    facilityId),
                Times.Once);
        }
    }
}