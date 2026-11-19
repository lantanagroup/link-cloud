using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared;

[Trait("Category", "UnitTests")]
public class RetryJobTests
{
    [Fact]
    public async Task Execute_PayloadSubmitted_PublishesTheReportKeyThenDeletesTheJob()
    {
        var scheduleId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var model = Model(nameof(KafkaTopic.PayloadSubmitted), KafkaKeys.ForReport("facility-1", scheduleId));
        var producer = new Mock<IProducer<string, string>>();
        string? topic = null;
        string? key = null;
        producer
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, string>, CancellationToken>((publishedTopic, message, _) =>
            {
                topic = publishedTopic;
                key = message.Key;
            })
            .ReturnsAsync(new DeliveryResult<string, string> { Status = PersistenceStatus.Persisted });
        var scheduler = new Mock<IScheduler>();
        scheduler.Setup(s => s.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var job = Job(producer, scheduler, out var context, model);

        await job.Execute(context);

        Assert.Equal(nameof(KafkaTopic.PayloadSubmitted), topic);
        Assert.Equal(KafkaKeys.ForReport("facility-1", scheduleId), key);
        scheduler.Verify(s => s.DeleteJob(new JobKey(model.JobId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_PublishRejected_KeepsTheJobAndAsksQuartzToRunItAgain()
    {
        var model = Model("PatientEvent", "facility-key");
        var producer = new Mock<IProducer<string, string>>();
        producer
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProduceException<string, string>(
                new Error(ErrorCode.Local_MsgTimedOut, "delivery failed"),
                new DeliveryResult<string, string>()));
        var scheduler = new Mock<IScheduler>();
        var job = Job(producer, scheduler, out var context, model);

        var ex = await Assert.ThrowsAsync<JobExecutionException>(() => job.Execute(context));

        Assert.True(ex.RefireImmediately);
        scheduler.Verify(s => s.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static RetryJob Job(
        Mock<IProducer<string, string>> producer,
        Mock<IScheduler> scheduler,
        out IJobExecutionContext context,
        RetryModel model)
    {
        var factory = new Mock<IKafkaProducerFactory<string, string>>();
        factory
            .Setup(f => f.CreateProducer(It.IsAny<ProducerConfig>(), null, null, false))
            .Returns(producer.Object);
        var schedulerFactory = new Mock<ISchedulerFactory>();
        schedulerFactory.Setup(s => s.GetScheduler(It.IsAny<CancellationToken>())).ReturnsAsync(scheduler.Object);
        var map = new JobDataMap();
        map.PutObject("RetryModel", model);
        var trigger = new Mock<ITrigger>();
        trigger.SetupGet(t => t.JobDataMap).Returns(map);
        var execution = new Mock<IJobExecutionContext>();
        execution.SetupGet(c => c.Trigger).Returns(trigger.Object);
        execution.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        context = execution.Object;
        return new RetryJob(
            Mock.Of<ILogger<RetryJob>>(),
            factory.Object,
            schedulerFactory.Object,
            Mock.Of<IServiceScopeFactory>());
    }

    private static RetryModel Model(string topic, string key) =>
        new()
        {
            FacilityId = "facility-1",
            ServiceName = "Report",
            Topic = topic,
            Key = key,
            Value = "{\"facilityId\":\"facility-1\"}",
            Headers = new Dictionary<string, string>()
        };
}
