using Confluent.Kafka;
using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.Notification.Commands;
using LantanaGroup.Link.Notification.Application.Notification.Queries;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries;
using LantanaGroup.Link.Notification.Domain.Entities;
using LantanaGroup.Link.Notification.Listeners;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace UnitTests.Notification;

[Trait("Category", "UnitTests")]
public class NotificationRequestedListenerTests
{
    [Fact]
    public async Task ProcessingException_GoesToTheErrorTopic_AndTheNextMessageIsStillConsumed()
    {
        var consumed = 0;
        var consumer = new Mock<IConsumer<string, NotificationMessage>>();
        consumer
            .Setup(c => c.Consume(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                var call = Interlocked.Increment(ref consumed);
                if (call == 1)
                {
                    return Message("first");
                }

                if (call == 2)
                {
                    return Message("second");
                }

                throw new OperationCanceledException();
            });

        var factory = new Mock<IKafkaConsumerFactory>();
        factory
            .Setup(f => f.CreateNotificationRequestedConsumer(false, It.IsAny<KafkaAssignmentTracker>()))
            .Returns(consumer.Object);

        var creates = 0;
        var create = new Mock<ICreateNotificationCommand>();
        create
            .Setup(c => c.Execute(It.IsAny<CreateNotificationModel>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                if (Interlocked.Increment(ref creates) == 1)
                {
                    throw new InvalidOperationException("notification store unavailable");
                }

                return Task.FromResult(Guid.NewGuid().ToString());
            });

        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var send = new Mock<ISendNotificationCommand>();
        send
            .Setup(c => c.Execute(It.IsAny<SendNotificationModel>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                sent.TrySetResult();
                return Task.FromResult(true);
            });

        var validate = new Mock<IValidateEmailAddressCommand>();
        validate
            .Setup(c => c.Execute(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var get = new Mock<IGetNotificationQuery>();
        get
            .Setup(q => q.Execute(It.IsAny<NotificationId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationModel
            {
                Id = Guid.NewGuid().ToString(),
                Subject = "subject",
                Body = "body",
                Recipients = ["a@b.test"]
            });

        var services = new Mock<IServiceProvider>();
        services.Setup(s => s.GetService(typeof(IValidateEmailAddressCommand))).Returns(validate.Object);
        services.Setup(s => s.GetService(typeof(ICreateNotificationCommand))).Returns(create.Object);
        services.Setup(s => s.GetService(typeof(IGetNotificationQuery))).Returns(get.Object);
        services.Setup(s => s.GetService(typeof(ISendNotificationCommand))).Returns(send.Object);
        services.Setup(s => s.GetService(typeof(IGetFacilityConfigurationQuery))).Returns(Mock.Of<IGetFacilityConfigurationQuery>());
        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(services.Object);
        var scopes = new Mock<IServiceScopeFactory>();
        scopes.Setup(s => s.CreateScope()).Returns(scope.Object);

        var deadLettered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ConsumeResult<string, NotificationMessage>? failed = null;
        var deadLetter = new Mock<IDeadLetterExceptionHandler<NotificationRequestedListener, string, NotificationMessage>>();
        deadLetter.SetupProperty(h => h.Topic);
        deadLetter
            .Setup(h => h.HandleException(
                It.IsAny<ConsumeResult<string, NotificationMessage>>(),
                It.IsAny<DeadLetterException>(),
                It.IsAny<string>()))
            .Callback<ConsumeResult<string, NotificationMessage>, DeadLetterException, string>((result, _, _) =>
            {
                failed = result;
                deadLettered.TrySetResult();
            })
            .Returns(true);

        var notifications = new Mock<INotificationFactory>();
        notifications
            .Setup(f => f.CreateNotificationModelCreate(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<string>>(),
                It.IsAny<List<string>?>()))
            .Returns(new CreateNotificationModel());
        notifications
            .Setup(f => f.CreateSendNotificationModel(
                It.IsAny<string>(),
                It.IsAny<List<string>>(),
                It.IsAny<List<string>?>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(new SendNotificationModel());

        var listener = new NotificationRequestedListener(
            Mock.Of<ILogger<NotificationRequestedListener>>(),
            notifications.Object,
            factory.Object,
            scopes.Object,
            deadLetter.Object);

        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await listener.StartAsync(stop.Token);

        await Task.WhenAll(deadLettered.Task, sent.Task).WaitAsync(TimeSpan.FromSeconds(7));

        Assert.Equal("NotificationRequested-Error", deadLetter.Object.Topic);
        Assert.Equal("first", failed?.Message.Value.Subject);
        Assert.Null(failed?.Message.Key);
        deadLetter.Verify(
            h => h.HandleException(
                It.IsAny<ConsumeResult<string, NotificationMessage>>(),
                It.IsAny<DeadLetterException>(),
                It.IsAny<string>()),
            Times.Once);
        Assert.True(consumed >= 2);
    }

    [Fact]
    public async Task RepeatingConsumeFailure_WaitsBeforeReadingAgain()
    {
        var stamps = new List<DateTime>();
        var secondRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var consumer = new Mock<IConsumer<string, NotificationMessage>>();
        consumer
            .Setup(c => c.Consume(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                stamps.Add(DateTime.UtcNow);
                if (stamps.Count == 1)
                {
                    throw new InvalidOperationException("broker unavailable");
                }

                secondRead.TrySetResult();
                throw new OperationCanceledException();
            });

        var factory = new Mock<IKafkaConsumerFactory>();
        factory
            .Setup(f => f.CreateNotificationRequestedConsumer(false, It.IsAny<KafkaAssignmentTracker>()))
            .Returns(consumer.Object);

        var deadLetter = new Mock<IDeadLetterExceptionHandler<NotificationRequestedListener, string, NotificationMessage>>();
        deadLetter.SetupProperty(h => h.Topic);

        var listener = new NotificationRequestedListener(
            Mock.Of<ILogger<NotificationRequestedListener>>(),
            Mock.Of<INotificationFactory>(),
            factory.Object,
            Mock.Of<IServiceScopeFactory>(),
            deadLetter.Object);

        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await listener.StartAsync(stop.Token);
        await secondRead.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(stamps.Count >= 2);
        Assert.True(stamps[1] - stamps[0] >= TimeSpan.FromMilliseconds(750));
    }

    [Fact]
    public async Task ErrorTopicPublishFailure_DoesNotCommitAndRewinds()
    {
        var consumed = 0;
        var consumer = new Mock<IConsumer<string, NotificationMessage>>();
        consumer
            .Setup(c => c.Consume(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                var call = Interlocked.Increment(ref consumed);
                if (call == 1)
                {
                    return Message("first");
                }

                throw new OperationCanceledException();
            });

        var factory = new Mock<IKafkaConsumerFactory>();
        factory
            .Setup(f => f.CreateNotificationRequestedConsumer(false, It.IsAny<KafkaAssignmentTracker>()))
            .Returns(consumer.Object);

        var create = new Mock<ICreateNotificationCommand>();
        create
            .Setup(c => c.Execute(It.IsAny<CreateNotificationModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("notification store unavailable"));

        var validate = new Mock<IValidateEmailAddressCommand>();
        validate
            .Setup(c => c.Execute(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var services = new Mock<IServiceProvider>();
        services.Setup(s => s.GetService(typeof(IValidateEmailAddressCommand))).Returns(validate.Object);
        services.Setup(s => s.GetService(typeof(ICreateNotificationCommand))).Returns(create.Object);
        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(services.Object);
        var scopes = new Mock<IServiceScopeFactory>();
        scopes.Setup(s => s.CreateScope()).Returns(scope.Object);

        var deadLetter = new Mock<IDeadLetterExceptionHandler<NotificationRequestedListener, string, NotificationMessage>>();
        deadLetter.SetupProperty(h => h.Topic);
        deadLetter
            .Setup(h => h.HandleException(
                It.IsAny<ConsumeResult<string, NotificationMessage>>(),
                It.IsAny<DeadLetterException>(),
                It.IsAny<string>()))
            .Returns(false);

        var notifications = new Mock<INotificationFactory>();
        notifications
            .Setup(f => f.CreateNotificationModelCreate(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<string>>(),
                It.IsAny<List<string>?>()))
            .Returns(new CreateNotificationModel());

        var listener = new NotificationRequestedListener(
            Mock.Of<ILogger<NotificationRequestedListener>>(),
            notifications.Object,
            factory.Object,
            scopes.Object,
            deadLetter.Object);

        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await listener.StartAsync(stop.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(1200));

        consumer.Verify(
            c => c.Seek(It.Is<TopicPartitionOffset>(offset => offset.Topic == "NotificationRequested" && offset.Offset.Value == 0)),
            Times.AtLeastOnce);
        consumer.Verify(c => c.Commit(It.IsAny<ConsumeResult<string, NotificationMessage>>()), Times.Never);
    }

    private static ConsumeResult<string, NotificationMessage> Message(string subject) =>
        new()
        {
            Topic = "NotificationRequested",
            Partition = 0,
            Offset = 0,
            Message = new Message<string, NotificationMessage>
            {
                Key = null,
                Headers = new Headers(),
                Value = new NotificationMessage
                {
                    NotificationType = "Email",
                    Subject = subject,
                    Body = "body",
                    Recipients = ["a@b.test"]
                }
            }
        };
}
