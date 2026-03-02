using Confluent.Kafka;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Jobs;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report
{
    [Collection("ReportIntegrationTests")]
    public class ReportJobsIntegrationTests : IClassFixture<ReportIntegrationTestFixture>
    {
        private readonly ReportIntegrationTestFixture _fixture;

        public ReportJobsIntegrationTests(ReportIntegrationTestFixture fixture)
        {
            _fixture = fixture;
        }

        private Mock<IJobExecutionContext> CreateJobContext(string scheduleId)
        {
            var context = new Mock<IJobExecutionContext>();
            var jobDetail = new Mock<IJobDetail>();

            // Store as JSON string so JobDataMap.GetObject<string> can deserialize it
            var dataMap = new JobDataMap
            {
                { "ReportScheduleId", JsonSerializer.Serialize(scheduleId) }
            };

            jobDetail.Setup(j => j.JobDataMap).Returns(dataMap);
            context.Setup(c => c.JobDetail).Returns(jobDetail.Object);
            context.Setup(c => c.Trigger.JobDataMap).Returns(new JobDataMap());

            return context;
        }

        [Fact]
        public async Task Execute_ManifestProducedSuccessfully_UpdatesScheduleAndDeletesJob()
        {
            _fixture.SubmitPayloadKafkaProducerMock.Reset();
            _fixture.SchedulerFactoryMock.Reset();
            _fixture.TenantApiServiceMock.Setup(x => x.GetFacilityConfig(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FacilityModel { FacilityName = "Test Facility" });

            var schedulerMock = new Mock<IScheduler>();
            _fixture.SchedulerFactoryMock.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
                .ReturnsAsync(schedulerMock.Object);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

            var scheduleId = Guid.NewGuid().ToString();
            var schedule = new ReportSchedule
            {
                Id = scheduleId,
                FacilityId = "test-facility",
                ReportStartDate = DateTime.UtcNow.AddDays(-30),
                ReportEndDate = DateTime.UtcNow.AddDays(30),
                Frequency = Frequency.Monthly,
                ReportTypes = new List<string> { "DE-111" },
                Status = ScheduleStatus.Scheduled,
                EndOfReportPeriodJobHasRun = true,
                CreateDate = DateTime.UtcNow
            };
            await reportScheduledManager.AddAsync(schedule, CancellationToken.None);

            var job = new EndOfReportPeriodJob(
                Mock.Of<ILogger<EndOfReportPeriodJob>>(),
                _fixture.SchedulerFactoryMock.Object,
                _fixture.ScopeFactory,
                scope.ServiceProvider.GetRequiredService<DataAcquisitionRequestedProducer>(),
                scope.ServiceProvider.GetRequiredService<ReadyForValidationProducer>());

            var context = CreateJobContext(scheduleId);

            await job.Execute(context.Object);

            using var verifyScope = _fixture.ScopeFactory.CreateScope();
            var verifyManager = verifyScope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
            var updated = await verifyManager.SingleOrDefaultAsync(x => x.Id == scheduleId);

            Assert.Equal(ScheduleStatus.EndOfPeriod, updated.Status);
            Assert.True(updated.EndOfReportPeriodJobHasRun);
        }

        [Fact]
        public async Task Execute_ManifestNotProducedAndPatientsExist_ProducesDataAcquisitionThenUpdatesSchedule()
        {
            _fixture.DataAcquisitionRequestedKafkaProducerMock.Reset();
            _fixture.SchedulerFactoryMock.Reset();
            _fixture.TenantApiServiceMock.Setup(x => x.GetFacilityConfig(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FacilityModel { FacilityName = "Test Facility" });

            var schedulerMock = new Mock<IScheduler>();
            _fixture.SchedulerFactoryMock.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
                .ReturnsAsync(schedulerMock.Object);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
            var reportEntryManager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

            var scheduleId = Guid.NewGuid().ToString();
            var schedule = new ReportSchedule
            {
                Id = scheduleId,
                FacilityId = "test-facility",
                ReportStartDate = DateTime.UtcNow.AddDays(-30),
                ReportEndDate = DateTime.UtcNow.AddDays(30),
                Frequency = Frequency.Monthly,
                ReportTypes = new List<string> { "DE-111" },
                Status = ScheduleStatus.Scheduled,
                EndOfReportPeriodJobHasRun = true,
                CreateDate = DateTime.UtcNow
            };
            await reportScheduledManager.AddAsync(schedule, CancellationToken.None);

            var entry = new ReportEntry
            {
                PatientId = "pat-001",
                ReportScheduleId = scheduleId,
                FacilityId = "test-facility",
                ReportingStatus = ReportingStatus.PatientIdentified,
                CreateDate = DateTime.UtcNow
            };
            await reportEntryManager.AddAsync(entry, CancellationToken.None);

            var job = new EndOfReportPeriodJob(
                Mock.Of<ILogger<EndOfReportPeriodJob>>(),
                _fixture.SchedulerFactoryMock.Object,
                _fixture.ScopeFactory,
                scope.ServiceProvider.GetRequiredService<DataAcquisitionRequestedProducer>(),
                scope.ServiceProvider.GetRequiredService<ReadyForValidationProducer>());

            var context = CreateJobContext(scheduleId);

            await job.Execute(context.Object);

            _fixture.DataAcquisitionRequestedKafkaProducerMock.Verify(
                p => p.Produce(
                    It.IsAny<string>(),
                    It.Is<Message<string, DataAcquisitionRequestedValue>>(m => m.Key == "test-facility"),
                    It.IsAny<Action<DeliveryReport<string, DataAcquisitionRequestedValue>>>()),
                Times.Once);

            using var verifyScope = _fixture.ScopeFactory.CreateScope();
            var verifyManager = verifyScope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
            var updated = await verifyManager.SingleOrDefaultAsync(x => x.Id == scheduleId);

            Assert.Equal(ScheduleStatus.EndOfPeriod, updated.Status);
        }

    }
}