using Confluent.Kafka;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LanatanGroup.Link.QueryDispatch.Jobs;
using QueryDispatch.Application.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using QueryDispatch.Application.Interfaces;
using QueryDispatch.Domain.Managers;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.QueryDispatch;

[Trait("Category", "UnitTests")]
public class QueryDispatchJobMetricsTests
{
    [Fact]
    public async Task Execute_RecordsSuccessMetricsWhenProduceSucceeds()
    {
        var metrics = new Mock<IQueryDispatchServiceMetrics>();
        var acquisitionProducer = new Mock<IProducer<string, DataAcquisitionRequestedValue>>();
        var auditProducer = new Mock<IProducer<string, AuditEventMessage>>();
        Message<string, AuditEventMessage>? producedAudit = null;
        Message<string, DataAcquisitionRequestedValue>? producedAcquisition = null;
        auditProducer
            .Setup(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<Message<string, AuditEventMessage>>(),
                It.IsAny<Action<DeliveryReport<string, AuditEventMessage>>>()))
            .Callback<string, Message<string, AuditEventMessage>, Action<DeliveryReport<string, AuditEventMessage>>>((_, message, _) => producedAudit = message);
        acquisitionProducer
            .Setup(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<Message<string, DataAcquisitionRequestedValue>>(),
                It.IsAny<Action<DeliveryReport<string, DataAcquisitionRequestedValue>>>()))
            .Callback<string, Message<string, DataAcquisitionRequestedValue>, Action<DeliveryReport<string, DataAcquisitionRequestedValue>>>((_, message, _) => producedAcquisition = message);
        var patientDispatchMgr = new Mock<IPatientDispatchManager>();
        patientDispatchMgr
            .Setup(m => m.deletePatientDispatch(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var services = new Mock<IServiceProvider>();
        services.Setup(s => s.GetService(typeof(IPatientDispatchManager))).Returns(patientDispatchMgr.Object);
        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(services.Object);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        var job = new QueryDispatchJob(
            Mock.Of<ILogger<QueryDispatchJob>>(),
            scopeFactory.Object,
            acquisitionProducer.Object,
            auditProducer.Object,
            metrics.Object);

        var entity = new PatientDispatchEntity
        {
            FacilityId = "facility-1",
            PatientId = "patient-1",
            CorrelationId = "corr-1",
            ScheduledReportPeriods = []
        };

        var triggerMap = new JobDataMap();
        triggerMap.PutObject("PatientDispatchEntity", entity);
        var trigger = Mock.Of<ITrigger>(t => t.JobDataMap == triggerMap);
        var context = Mock.Of<IJobExecutionContext>(c => c.Trigger == trigger);

        await job.Execute(context);

        metrics.Verify(m => m.IncrementPatientsDispatched("facility-1", "success"), Times.Once);
        metrics.Verify(m => m.RecordDispatchDuration("facility-1", It.Is<double>(d => d >= 0)), Times.Once);

        Assert.NotNull(producedAudit);
        Assert.Equal(KafkaKeys.ForAudit("facility-1", "patient-1", QueryDispatchConstants.ServiceName), producedAudit!.Key);
        Assert.Equal("facility-1", producedAudit.Value.FacilityId);
        Assert.Equal("patient-1", producedAudit.Value.PatientId);
        Assert.NotNull(producedAcquisition);
        Assert.Equal(KafkaKeys.ForPatient("facility-1", "patient-1"), producedAcquisition!.Key);
        Assert.Equal("facility-1", producedAcquisition.Value.FacilityId);
        Assert.Equal("patient-1", producedAcquisition.Value.PatientId);
    }
}
