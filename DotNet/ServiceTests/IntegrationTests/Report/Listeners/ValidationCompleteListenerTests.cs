using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Listeners;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Linq.Expressions;
using System.Text;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report
{
    [Collection("ReportIntegrationTests")]
    [Trait("Category", "IntegrationTests")]
    public class ValidationCompleteListenerTests
    {
        private readonly ReportIntegrationTestFixture _fixture;
        private readonly ITestOutputHelper _output;

        public ValidationCompleteListenerTests(ReportIntegrationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task ProcessMessageAsync_ValidValidation_UpdatesStatusAndProducesSubmitPayload()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            var submitPayloadProducer = scope.ServiceProvider.GetRequiredService<SubmitPayloadProducer>();
            var reportManifestProducer = scope.ServiceProvider.GetRequiredService<ReportManifestProducer>();
            var serviceScopeFactory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();

            // Setup schedule and entry in DB
            var schedule = new ReportScheduleModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = "TestFacility",
                ReportStartDate = DateTime.UtcNow.AddDays(-30),
                ReportEndDate = DateTime.UtcNow,
                ReportTypes = new List<string> { "TestReport" },
                Frequency = Frequency.Monthly,
                PayloadRootUri = "test://payload/root"
            };
            await database.ReportScheduledRepository.AddAsync(schedule);

            var entry = new MeasureReportSubmissionEntryModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = schedule.FacilityId,
                ReportScheduleId = schedule.Id,
                PatientId = "Patient1",
                Status = PatientSubmissionStatus.ValidationRequested,
                PayloadUri = "test://payload/patient1"
            };
            await database.SubmissionEntryRepository.AddAsync(entry);

            // Create listener
            var listener = new ValidationCompleteListener(
                scope.ServiceProvider.GetRequiredService<ILogger<ValidationCompleteListener>>(),
                scope.ServiceProvider.GetRequiredService<IKafkaConsumerFactory<string, ValidationCompleteValue>>(),
                scope.ServiceProvider.GetRequiredService<ITransientExceptionHandler<string, ValidationCompleteValue>>(),
                scope.ServiceProvider.GetRequiredService<IDeadLetterExceptionHandler<string, ValidationCompleteValue>>(),
                submitPayloadProducer,
                serviceScopeFactory,
                scope.ServiceProvider.GetRequiredService<BlobStorageService>(),
                scope.ServiceProvider.GetRequiredService<PatientReportSubmissionBundler>(),
                reportManifestProducer);

            // Simulate ConsumeResult
            var headers = new Headers { { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) } };
            var message = new Message<string, ValidationCompleteValue>
            {
                Key = schedule.FacilityId,
                Value = new ValidationCompleteValue { ReportTrackingId = schedule.Id, PatientId = "Patient1", IsValid = true },
                Headers = headers
            };
            var consumeResult = new ConsumeResult<string, ValidationCompleteValue> { Message = message, Topic = nameof(KafkaTopic.ValidationComplete) };

            // Execute
            await listener.ProcessMessageAsync(consumeResult, default);

            // Asserts (using real DB to check updates)
            var updatedEntry = await database.SubmissionEntryRepository.FirstOrDefaultAsync(e => e.Id == entry.Id);
            Assert.NotNull(updatedEntry);
            Assert.Equal(PatientSubmissionStatus.ValidationComplete, updatedEntry.Status);
            Assert.Equal(ValidationStatus.Passed, updatedEntry.ValidationStatus);

            ReportIntegrationTestFixture.SubmitPayloadProducerMock.Verify(p => p.Produce(
                It.Is<string>(topic => topic == nameof(KafkaTopic.SubmitPayload)),
                It.Is<Message<SubmitPayloadKey, SubmitPayloadValue>>(m =>
                    m.Key.FacilityId == schedule.FacilityId &&
                    m.Key.ReportScheduleId == schedule.Id &&
                    m.Value.PayloadType == PayloadType.MeasureReportSubmissionEntry &&
                    m.Value.PatientId == "Patient1" &&
                    m.Value.PayloadUri == "test://payload/patient1"),
                It.IsAny<Action<DeliveryReport<SubmitPayloadKey, SubmitPayloadValue>>>()), Times.Once());
            ReportIntegrationTestFixture.SubmitPayloadProducerMock.Verify(p => p.Produce(
                It.Is<string>(topic => topic == nameof(KafkaTopic.SubmitPayload)),
                It.Is<Message<SubmitPayloadKey, SubmitPayloadValue>>(m =>
                    m.Key.FacilityId == schedule.FacilityId &&
                    m.Key.ReportScheduleId == schedule.Id &&
                    m.Value.PayloadType == PayloadType.ReportSchedule &&
                    m.Value.PayloadUri == "test://payload/root"),
                It.IsAny<Action<DeliveryReport<SubmitPayloadKey, SubmitPayloadValue>>>()), Times.Once());
        }

        [Fact]
        public async Task ProcessMessageAsync_InvalidValidation_AddsOutcomeUpdatesBlobAndProducesSubmitPayload()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            var submitPayloadProducer = scope.ServiceProvider.GetRequiredService<SubmitPayloadProducer>();
            var reportManifestProducer = scope.ServiceProvider.GetRequiredService<ReportManifestProducer>();
            var serviceScopeFactory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();

            // Setup schedule and entry
            var schedule = new ReportScheduleModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = "TestFacility",
                ReportStartDate = DateTime.UtcNow.AddDays(-30),
                ReportEndDate = DateTime.UtcNow,
                ReportTypes = new List<string> { "TestReport" },
                Frequency = Frequency.Monthly,
                PayloadRootUri = "test://payload/root"
            };
            await database.ReportScheduledRepository.AddAsync(schedule);

            var entry = new MeasureReportSubmissionEntryModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = schedule.FacilityId,
                ReportScheduleId = schedule.Id,
                PatientId = "Patient1",
                Status = PatientSubmissionStatus.ValidationRequested,
                PayloadUri = "test://payload/patient1",
                MeasureReport = new MeasureReport
                {
                    Id = Guid.NewGuid().ToString(),
                    Measure = "TestMeasure",
                    Status = MeasureReport.MeasureReportStatus.Complete,
                    Type = MeasureReport.MeasureReportType.Individual
                },
                ContainedResources = new List<MeasureReportSubmissionEntryModel.ContainedResource>()
            };
            await database.SubmissionEntryRepository.AddAsync(entry);

            // Create listener
            var listener = new ValidationCompleteListener(
                scope.ServiceProvider.GetRequiredService<ILogger<ValidationCompleteListener>>(),
                scope.ServiceProvider.GetRequiredService<IKafkaConsumerFactory<string, ValidationCompleteValue>>(),
                scope.ServiceProvider.GetRequiredService<ITransientExceptionHandler<string, ValidationCompleteValue>>(),
                scope.ServiceProvider.GetRequiredService<IDeadLetterExceptionHandler<string, ValidationCompleteValue>>(),
                submitPayloadProducer,
                serviceScopeFactory,
                scope.ServiceProvider.GetRequiredService<BlobStorageService>(),
                scope.ServiceProvider.GetRequiredService<PatientReportSubmissionBundler>(),
                reportManifestProducer);

            // ConsumeResult with IsValid = false
            var headers = new Headers { { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) } };
            var message = new Message<string, ValidationCompleteValue>
            {
                Key = schedule.FacilityId,
                Value = new ValidationCompleteValue { ReportTrackingId = schedule.Id, PatientId = "Patient1", IsValid = false },
                Headers = headers
            };
            var consumeResult = new ConsumeResult<string, ValidationCompleteValue> { Message = message, Topic = nameof(KafkaTopic.ValidationComplete) };

            // Execute
            await listener.ProcessMessageAsync(consumeResult, default);

            // Asserts
            var updatedEntry = await database.SubmissionEntryRepository.FirstOrDefaultAsync(e => e.Id == entry.Id);
            Assert.NotNull(updatedEntry);
            Assert.Equal(PatientSubmissionStatus.ValidationComplete, updatedEntry.Status);
            Assert.Equal(ValidationStatus.Failed, updatedEntry.ValidationStatus);
            Assert.Equal("test://updated/uri", updatedEntry.PayloadUri);
            Assert.Contains(updatedEntry.ContainedResources, cr => cr.ResourceType == "OperationOutcome");

            // Verify PatientResourceModel was created in the database
            var createdResource = await database.PatientResourceRepository.FirstOrDefaultAsync(r =>
                r.FacilityId == schedule.FacilityId &&
                r.PatientId == "Patient1" &&
                r.ResourceType == "OperationOutcome");
            Assert.NotNull(createdResource);
            Assert.IsType<OperationOutcome>(createdResource.GetResource());
            Assert.Equal("Patient has failed Validation", ((OperationOutcome)createdResource.GetResource()).Issue.First().Diagnostics);

            ReportIntegrationTestFixture.SubmitPayloadProducerMock.Verify(p => p.Produce(
                It.Is<string>(topic => topic == nameof(KafkaTopic.SubmitPayload)),
                It.Is<Message<SubmitPayloadKey, SubmitPayloadValue>>(m =>
                    m.Key.FacilityId == schedule.FacilityId &&
                    m.Key.ReportScheduleId == schedule.Id &&
                    m.Value.PayloadType == PayloadType.MeasureReportSubmissionEntry &&
                    m.Value.PatientId == "Patient1" &&
                    m.Value.PayloadUri == "test://updated/uri"),
                It.IsAny<Action<DeliveryReport<SubmitPayloadKey, SubmitPayloadValue>>>()), Times.Once());
            ReportIntegrationTestFixture.SubmitPayloadProducerMock.Verify(p => p.Produce(
                It.Is<string>(topic => topic == nameof(KafkaTopic.SubmitPayload)),
                It.Is<Message<SubmitPayloadKey, SubmitPayloadValue>>(m =>
                    m.Key.FacilityId == schedule.FacilityId &&
                    m.Key.ReportScheduleId == schedule.Id &&
                    m.Value.PayloadType == PayloadType.ReportSchedule &&
                    m.Value.PayloadUri == "test://payload/root"),
                It.IsAny<Action<DeliveryReport<SubmitPayloadKey, SubmitPayloadValue>>>()), Times.Once());
        }

        [Fact]
        public async Task ProcessMessageAsync_NoScheduleFound_ThrowsDeadLetterException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            var submitPayloadProducer = scope.ServiceProvider.GetRequiredService<SubmitPayloadProducer>();
            var reportManifestProducer = scope.ServiceProvider.GetRequiredService<ReportManifestProducer>();

            // Mock IServiceScopeFactory to override IReportScheduledManager for error
            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockScope = new Mock<IServiceScope>();
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
            mockServiceProvider.Setup(sp => sp.GetService(typeof(IReportScheduledManager))).Returns(new Mock<IReportScheduledManager>().Object);
            mockServiceProvider.Setup(sp => sp.GetService(It.Is<Type>(t => t != typeof(IReportScheduledManager)))).Returns<Type>(t => scope.ServiceProvider.GetService(t));
            mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
            var reportScheduledManagerMock = mockServiceProvider.Object.GetService<IReportScheduledManager>();
            Mock.Get(reportScheduledManagerMock).Setup(m => m.SingleOrDefaultAsync(It.IsAny<Expression<Func<ReportScheduleModel, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync((ReportScheduleModel)null);

            // Create listener
            var listener = new ValidationCompleteListener(
                scope.ServiceProvider.GetRequiredService<ILogger<ValidationCompleteListener>>(),
                scope.ServiceProvider.GetRequiredService<IKafkaConsumerFactory<string, ValidationCompleteValue>>(),
                scope.ServiceProvider.GetRequiredService<ITransientExceptionHandler<string, ValidationCompleteValue>>(),
                scope.ServiceProvider.GetRequiredService<IDeadLetterExceptionHandler<string, ValidationCompleteValue>>(),
                submitPayloadProducer,
                mockScopeFactory.Object,
                scope.ServiceProvider.GetRequiredService<BlobStorageService>(),
                scope.ServiceProvider.GetRequiredService<PatientReportSubmissionBundler>(),
                reportManifestProducer);

            // Simulate ConsumeResult
            var consumeResult = new ConsumeResult<string, ValidationCompleteValue>
            {
                Message = new Message<string, ValidationCompleteValue>
                {
                    Key = "TestFacility",
                    Value = new ValidationCompleteValue { ReportTrackingId = "nonexistent", PatientId = "Patient1", IsValid = true },
                    Headers = new Headers { { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) } }
                },
                Topic = nameof(KafkaTopic.ValidationComplete)
            };

            // Execute and assert
            var exception = await Assert.ThrowsAsync<DeadLetterException>(() => listener.ProcessMessageAsync(consumeResult, default));
            Assert.Contains("No ReportSchedule found", exception.Message);
        }

        [Fact]
        public async Task ProcessMessageAsync_TimeoutException_ThrowsTimeoutException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            var submitPayloadProducer = scope.ServiceProvider.GetRequiredService<SubmitPayloadProducer>();
            var reportManifestProducer = scope.ServiceProvider.GetRequiredService<ReportManifestProducer>();

            // Mock IServiceScopeFactory to override IReportScheduledManager for error
            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockScope = new Mock<IServiceScope>();
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
            mockServiceProvider.Setup(sp => sp.GetService(typeof(IReportScheduledManager))).Returns(new Mock<IReportScheduledManager>().Object);
            mockServiceProvider.Setup(sp => sp.GetService(It.Is<Type>(t => t != typeof(IReportScheduledManager)))).Returns<Type>(t => scope.ServiceProvider.GetService(t));
            mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
            var reportScheduledManagerMock = mockServiceProvider.Object.GetService<IReportScheduledManager>();
            Mock.Get(reportScheduledManagerMock).Setup(m => m.SingleOrDefaultAsync(It.IsAny<Expression<Func<ReportScheduleModel, bool>>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException());

            // Create listener
            var listener = new ValidationCompleteListener(
                scope.ServiceProvider.GetRequiredService<ILogger<ValidationCompleteListener>>(),
                scope.ServiceProvider.GetRequiredService<IKafkaConsumerFactory<string, ValidationCompleteValue>>(),
                scope.ServiceProvider.GetRequiredService<ITransientExceptionHandler<string, ValidationCompleteValue>>(),
                scope.ServiceProvider.GetRequiredService<IDeadLetterExceptionHandler<string, ValidationCompleteValue>>(),
                submitPayloadProducer,
                mockScopeFactory.Object,
                scope.ServiceProvider.GetRequiredService<BlobStorageService>(),
                scope.ServiceProvider.GetRequiredService<PatientReportSubmissionBundler>(),
                reportManifestProducer);

            // Simulate ConsumeResult
            var consumeResult = new ConsumeResult<string, ValidationCompleteValue>
            {
                Message = new Message<string, ValidationCompleteValue>
                {
                    Key = "TestFacility",
                    Value = new ValidationCompleteValue { ReportTrackingId = "testid", PatientId = "Patient1", IsValid = true },
                    Headers = new Headers { { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) } }
                },
                Topic = nameof(KafkaTopic.ValidationComplete)
            };

            // Execute and assert
            await Assert.ThrowsAsync<TimeoutException>(() => listener.ProcessMessageAsync(consumeResult, default));
        }

        [Fact]
        public async Task ProcessMessageAsync_GeneralException_ThrowsException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            var submitPayloadProducer = scope.ServiceProvider.GetRequiredService<SubmitPayloadProducer>();
            var reportManifestProducer = scope.ServiceProvider.GetRequiredService<ReportManifestProducer>();

            // Mock IServiceScopeFactory to override IReportScheduledManager for error
            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockScope = new Mock<IServiceScope>();
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
            mockServiceProvider.Setup(sp => sp.GetService(typeof(IReportScheduledManager))).Returns(new Mock<IReportScheduledManager>().Object);
            mockServiceProvider.Setup(sp => sp.GetService(It.Is<Type>(t => t != typeof(IReportScheduledManager)))).Returns<Type>(t => scope.ServiceProvider.GetService(t));
            mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
            var reportScheduledManagerMock = mockServiceProvider.Object.GetService<IReportScheduledManager>();
            Mock.Get(reportScheduledManagerMock).Setup(m => m.SingleOrDefaultAsync(It.IsAny<Expression<Func<ReportScheduleModel, bool>>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("Test error"));

            // Create listener
            var listener = new ValidationCompleteListener(
                scope.ServiceProvider.GetRequiredService<ILogger<ValidationCompleteListener>>(),
                scope.ServiceProvider.GetRequiredService<IKafkaConsumerFactory<string, ValidationCompleteValue>>(),
                scope.ServiceProvider.GetRequiredService<ITransientExceptionHandler<string, ValidationCompleteValue>>(),
                scope.ServiceProvider.GetRequiredService<IDeadLetterExceptionHandler<string, ValidationCompleteValue>>(),
                submitPayloadProducer,
                mockScopeFactory.Object,
                scope.ServiceProvider.GetRequiredService<BlobStorageService>(),
                scope.ServiceProvider.GetRequiredService<PatientReportSubmissionBundler>(),
                reportManifestProducer);

            // Simulate ConsumeResult
            var consumeResult = new ConsumeResult<string, ValidationCompleteValue>
            {
                Message = new Message<string, ValidationCompleteValue>
                {
                    Key = "TestFacility",
                    Value = new ValidationCompleteValue { ReportTrackingId = "testid", PatientId = "Patient1", IsValid = true },
                    Headers = new Headers { { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) } }
                },
                Topic = nameof(KafkaTopic.ValidationComplete)
            };

            // Execute and assert
            var exception = await Assert.ThrowsAsync<Exception>(() => listener.ProcessMessageAsync(consumeResult, default));
            Assert.Equal("Test error", exception.Message);
        }
    }
}