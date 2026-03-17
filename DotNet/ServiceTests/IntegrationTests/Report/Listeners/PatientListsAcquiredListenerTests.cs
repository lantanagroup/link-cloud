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
    public class PatientListsAcquiredListenerTests : IClassFixture<ReportIntegrationTestFixture>
    {
        private readonly ReportIntegrationTestFixture _fixture;

        public PatientListsAcquiredListenerTests(ReportIntegrationTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ProcessMessageAsync_ValidPatientList_CreatesReportEntries()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var listener = scope.ServiceProvider.GetRequiredService<PatientListsAcquiredListener>();
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

            var patientListMessage = new PatientListMessage
            {
                PatientLists = new List<PatientListItem>
                {
                    new PatientListItem
                    {
                        PatientIds = new List<string> { "Patient/12345", "Patient/67890" }
                    }
                }
            };

            var consumeResult = new ConsumeResult<string, PatientListMessage>
            {
                Message = new Message<string, PatientListMessage>
                {
                    Key = facilityId,
                    Value = patientListMessage
                }
            };

            await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

            var entry1 = await reportEntryManager.SingleOrDefaultAsync(e => e.PatientId == "12345" && e.ReportScheduleId == reportId);
            var entry2 = await reportEntryManager.SingleOrDefaultAsync(e => e.PatientId == "67890" && e.ReportScheduleId == reportId);

            Assert.NotNull(entry1);
            Assert.NotNull(entry2);
            Assert.Equal(ReportingStatus.PatientIdentified, entry1.ReportingStatus);
            Assert.Equal(2, entry1.MeasureReports.Count);
            Assert.Equal(2, entry2.MeasureReports.Count);
        }

        [Fact]
        public async Task ProcessMessageAsync_ExistingReportEntry_AddsMissingMeasureReportsOnly()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var listener = scope.ServiceProvider.GetRequiredService<PatientListsAcquiredListener>();
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

            var patientListMessage = new PatientListMessage
            {
                ReportTrackingId = schedule.Id.ToString(),
                PatientLists = new List<PatientListItem>
                {
                    new PatientListItem { PatientIds = new List<string> { "Patient/12345" } }
                }
            };

            var consumeResult = new ConsumeResult<string, PatientListMessage>
            {
                Message = new Message<string, PatientListMessage>
                {
                    Key = facilityId,
                    Value = patientListMessage
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
        public async Task ProcessMessageAsync_NullResult_DoesNothing()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var listener = scope.ServiceProvider.GetRequiredService<PatientListsAcquiredListener>();

            var consumeResult = (ConsumeResult<string, PatientListMessage>)null!;

            await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);
        }

        [Fact]
        public async Task ProcessMessageAsync_InvalidMessage_CallsDeadLetterHandler()
        {
            _fixture.PatientListsAcquiredDeadLetterHandlerMock.Reset();

            using var scope = _fixture.ScopeFactory.CreateScope();
            var listener = scope.ServiceProvider.GetRequiredService<PatientListsAcquiredListener>();

            var facilityId = "test-facility-004";

            var consumeResult = new ConsumeResult<string, PatientListMessage>
            {
                Message = new Message<string, PatientListMessage>
                {
                    Key = facilityId,
                    Value = new PatientListMessage { PatientLists = null }
                }
            };

            await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

            _fixture.PatientListsAcquiredDeadLetterHandlerMock.Verify(
                h => h.HandleException(
                    It.IsAny<ConsumeResult<string, PatientListMessage>>(),
                    It.IsAny<DeadLetterException>(),
                    facilityId),
                Times.Once);
        }

        [Fact]
        public async Task ProcessMessageAsync_EmptyKey_CallsDeadLetterHandler()
        {
            _fixture.PatientListsAcquiredDeadLetterHandlerMock.Reset();

            using var scope = _fixture.ScopeFactory.CreateScope();
            var listener = scope.ServiceProvider.GetRequiredService<PatientListsAcquiredListener>();

            var consumeResult = new ConsumeResult<string, PatientListMessage>
            {
                Message = new Message<string, PatientListMessage>
                {
                    Key = string.Empty,
                    Value = new PatientListMessage { PatientLists = new List<PatientListItem> { new PatientListItem { PatientIds = new List<string> { "Patient/1" } } } }
                }
            };

            await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

            _fixture.PatientListsAcquiredDeadLetterHandlerMock.Verify(h => h.HandleException(It.IsAny<ConsumeResult<string, PatientListMessage>>(), It.IsAny<DeadLetterException>(), string.Empty), Times.Once);
        }

        [Fact]
        public async Task ProcessMessageAsync_NoScheduledReports_CallsTransientHandler()
        {
            _fixture.PatientListsAcquiredTransientHandlerMock.Reset();

            using var scope = _fixture.ScopeFactory.CreateScope();
            var listener = scope.ServiceProvider.GetRequiredService<PatientListsAcquiredListener>();

            var facilityId = "test-facility-005";

            var patientListMessage = new PatientListMessage
            {
                PatientLists = new List<PatientListItem>
                {
                    new PatientListItem { PatientIds = new List<string> { "Patient/111" } }
                }
            };

            var consumeResult = new ConsumeResult<string, PatientListMessage>
            {
                Message = new Message<string, PatientListMessage>
                {
                    Key = facilityId,
                    Value = patientListMessage
                }
            };

            await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

            _fixture.PatientListsAcquiredTransientHandlerMock.Verify(
                h => h.HandleException(
                    It.IsAny<ConsumeResult<string, PatientListMessage>>(),
                    It.IsAny<TransientException>(),
                    facilityId),
                Times.Once);
        }

        [Fact]
        public async Task ProcessMessageAsync_GeneralException_CallsDeadLetterHandler()
        {
            _fixture.PatientListsAcquiredDeadLetterHandlerMock.Reset();

            using var scope = _fixture.ScopeFactory.CreateScope();
            var listener = scope.ServiceProvider.GetRequiredService<PatientListsAcquiredListener>();
            var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

            var facilityId = "test-facility-010";
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
                EndOfReportPeriodJobHasRun = false,
                CreateDate = DateTime.UtcNow
            };
            await reportScheduledManager.AddAsync(schedule, CancellationToken.None);

            var patientListMessage = new PatientListMessage
            {
                PatientLists = new List<PatientListItem>
                {
                    new PatientListItem
                    {
                        PatientIds = new List<string> { null! }   // causes NRE on pId.Split('/') → hits final catch (Exception ex)
                    }
                }
            };

            var consumeResult = new ConsumeResult<string, PatientListMessage>
            {
                Message = new Message<string, PatientListMessage>
                {
                    Key = facilityId,
                    Value = patientListMessage
                }
            };

            await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

            _fixture.PatientListsAcquiredDeadLetterHandlerMock.Verify(
                h => h.HandleException(
                    It.IsAny<ConsumeResult<string, PatientListMessage>>(),
                    It.Is<DeadLetterException>(ex => ex.Message.Contains("Report - PatientListsAcquired Exception thrown:")),
                    facilityId),
                Times.Once);
        }
    }
}