using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Auditing;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared.Auditing
{
    [Trait("Category", "UnitTests")]
    public class KafkaAuditPublisherTests
    {
        private static AuditEventMessage AnEvent() => new()
        {
            FacilityId = "TestFacility01",
            ServiceName = "Tenant",
            Action = AuditEventType.Update,
            Resource = "Facility",
            EventDate = DateTime.UtcNow
        };

        private static DeliveryResult<string, AuditEventMessage> Delivered(PersistenceStatus status) => new()
        {
            Status = status,
            Topic = nameof(KafkaTopic.AuditableEventOccurred)
        };

        private static Mock<IProducer<string, AuditEventMessage>> Producer() => new();

        private static (KafkaAuditPublisher Publisher, FakeAuditMetrics Metrics) Build(
            Mock<IProducer<string, AuditEventMessage>> producer, int maxAttempts = 3, TimeSpan? responseTimeout = null)
        {
            var metrics = new FakeAuditMetrics();
            var options = AuditTest.Options(configured =>
            {
                configured.Publish.MaxAttempts = maxAttempts;
                //the tests must not spend the backoff waiting.
                configured.Publish.InitialBackoff = TimeSpan.Zero;
                configured.Publish.ResponseTimeout = responseTimeout ?? TimeSpan.FromSeconds(5);
            });

            return (new KafkaAuditPublisher(producer.Object, metrics, options.AsOptions(),
                NullLogger<KafkaAuditPublisher>.Instance), metrics);
        }

        private static void Produces(Mock<IProducer<string, AuditEventMessage>> producer,
            PersistenceStatus status) =>
            producer
                .Setup(p => p.ProduceAsync(It.IsAny<string>(),
                    It.IsAny<Message<string, AuditEventMessage>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Delivered(status));

        private static void Throws(Mock<IProducer<string, AuditEventMessage>> producer, Exception exception) =>
            producer
                .Setup(p => p.ProduceAsync(It.IsAny<string>(),
                    It.IsAny<Message<string, AuditEventMessage>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);

        private static void VerifyAttempts(Mock<IProducer<string, AuditEventMessage>> producer, int expected) =>
            producer.Verify(p => p.ProduceAsync(It.IsAny<string>(),
                It.IsAny<Message<string, AuditEventMessage>>(), It.IsAny<CancellationToken>()),
                Times.Exactly(expected));

        [Fact]
        public async Task APersistedEvent_IsPublishedOnceAndCountsNoFailure()
        {
            var producer = Producer();
            Produces(producer, PersistenceStatus.Persisted);
            var (publisher, metrics) = Build(producer);

            await publisher.PublishAsync(AnEvent(), CancellationToken.None);

            producer.Verify(p => p.ProduceAsync(nameof(KafkaTopic.AuditableEventOccurred),
                It.IsAny<Message<string, AuditEventMessage>>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.Empty(metrics.Failures);
        }

        [Fact]
        public async Task TheEventIsKeyedByFacility_SoOneFacilitysEventsStayInOrder()
        {
            Message<string, AuditEventMessage>? sent = null;
            var producer = Producer();
            producer
                .Setup(p => p.ProduceAsync(It.IsAny<string>(),
                    It.IsAny<Message<string, AuditEventMessage>>(), It.IsAny<CancellationToken>()))
                .Callback<string, Message<string, AuditEventMessage>, CancellationToken>(
                    (_, message, _) => sent = message)
                .ReturnsAsync(Delivered(PersistenceStatus.Persisted));
            var (publisher, _) = Build(producer);

            await publisher.PublishAsync(AnEvent(), CancellationToken.None);

            Assert.NotNull(sent);
            Assert.Equal("TestFacility01", sent.Key);
        }

        [Fact]
        public async Task ATransientFailure_IsRetriedUntilItSucceeds()
        {
            var attempts = 0;
            var producer = Producer();
            producer
                .Setup(p => p.ProduceAsync(It.IsAny<string>(),
                    It.IsAny<Message<string, AuditEventMessage>>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    attempts++;
                    if (attempts < 3)
                    {
                        throw new KafkaException(ErrorCode.Local_Transport);
                    }

                    return Task.FromResult(Delivered(PersistenceStatus.Persisted));
                });
            var (publisher, metrics) = Build(producer);

            await publisher.PublishAsync(AnEvent(), CancellationToken.None);

            Assert.Equal(3, attempts);
            Assert.Empty(metrics.Failures);
        }

        [Fact]
        public async Task AnEventTheBrokerNeverConfirms_IsTreatedAsAFailure()
        {
            var producer = Producer();
            Produces(producer, PersistenceStatus.NotPersisted);
            var (publisher, metrics) = Build(producer, maxAttempts: 2);

            await publisher.PublishAsync(AnEvent(), CancellationToken.None);

            //an unconfirmed produce does not throw, so nothing else would notice it.
            VerifyAttempts(producer, 2);
            Assert.Equal("Facility", Assert.Single(metrics.Failures));
        }

        [Fact]
        public async Task AnEventThatNeverPublishes_CountsAFailureAndDoesNotFailTheRequest()
        {
            var producer = Producer();
            Throws(producer, new KafkaException(ErrorCode.Local_Transport));
            var (publisher, metrics) = Build(producer, maxAttempts: 3);

            //the handler's change is already committed; throwing here would turn a successful write
            //into a 500 and invite the caller to repeat it.
            await publisher.PublishAsync(AnEvent(), CancellationToken.None);

            VerifyAttempts(producer, 3);
            Assert.Equal("Facility", Assert.Single(metrics.Failures));
        }

        [Fact]
        public async Task ShutdownMidPublish_CountsTheLostEventRatherThanThrowing()
        {
            using var shuttingDown = new CancellationTokenSource();
            await shuttingDown.CancelAsync();

            var producer = Producer();
            Throws(producer, new OperationCanceledException());
            var (publisher, metrics) = Build(producer);

            await publisher.PublishAsync(AnEvent(), shuttingDown.Token);

            //no retry is attempted once the service is going down, but the loss is still recorded.
            VerifyAttempts(producer, 1);
            Assert.Equal("Facility", Assert.Single(metrics.Failures));
        }

        [Fact]
        public async Task OneAttempt_MeansNoRetry()
        {
            var producer = Producer();
            Throws(producer, new KafkaException(ErrorCode.Local_Transport));
            var (publisher, metrics) = Build(producer, maxAttempts: 1);

            await publisher.PublishAsync(AnEvent(), CancellationToken.None);

            VerifyAttempts(producer, 1);
            Assert.Single(metrics.Failures);
        }

        [Fact]
        public void TheFailureCounterKeepsTheNameTheStoryAndEveryQaCheckUse()
        {
            Assert.Equal("audit_publish_failed", AuditMetrics.PublishFailedCounter);
        }

        [Fact]
        public async Task ABrokerThatNeverAnswers_DoesNotHoldTheRequest()
        {
            var delivery = new TaskCompletionSource<DeliveryResult<string, AuditEventMessage>>();
            var producer = Producer();
            producer
                .Setup(p => p.ProduceAsync(It.IsAny<string>(),
                    It.IsAny<Message<string, AuditEventMessage>>(), It.IsAny<CancellationToken>()))
                .Returns(delivery.Task);
            var (publisher, metrics) = Build(producer, responseTimeout: TimeSpan.FromMilliseconds(100));

            //found against a paused broker in the local stack: the request waited out the outage.
            var publishing = publisher.PublishAsync(AnEvent(), CancellationToken.None);
            var finished = await Task.WhenAny(publishing, Task.Delay(TimeSpan.FromSeconds(5)));

            Assert.Same(publishing, finished);
            //nothing is lost yet: the producer still holds the message.
            Assert.Empty(metrics.Failures);
        }

        [Fact]
        public async Task ASlowProduce_IsNotRetried_SoTheEventIsNotDuplicatedWhenTheBrokerRecovers()
        {
            var delivery = new TaskCompletionSource<DeliveryResult<string, AuditEventMessage>>();
            var producer = Producer();
            producer
                .Setup(p => p.ProduceAsync(It.IsAny<string>(),
                    It.IsAny<Message<string, AuditEventMessage>>(), It.IsAny<CancellationToken>()))
                .Returns(delivery.Task);
            var (publisher, _) = Build(producer, maxAttempts: 3, responseTimeout: TimeSpan.FromMilliseconds(50));

            await publisher.PublishAsync(AnEvent(), CancellationToken.None);

            VerifyAttempts(producer, 1);
        }

        [Fact]
        public async Task AnEventConfirmedAfterTheRequestReturned_CountsNoFailure()
        {
            var delivery = new TaskCompletionSource<DeliveryResult<string, AuditEventMessage>>();
            var producer = Producer();
            producer
                .Setup(p => p.ProduceAsync(It.IsAny<string>(),
                    It.IsAny<Message<string, AuditEventMessage>>(), It.IsAny<CancellationToken>()))
                .Returns(delivery.Task);
            var (publisher, metrics) = Build(producer, responseTimeout: TimeSpan.FromMilliseconds(50));

            await publisher.PublishAsync(AnEvent(), CancellationToken.None);
            delivery.SetResult(Delivered(PersistenceStatus.Persisted));

            Assert.Empty(metrics.Failures);
        }

        [Fact]
        public async Task AnEventTheProducerFinallyGivesUpOn_IsCountedWhenItDoes()
        {
            var delivery = new TaskCompletionSource<DeliveryResult<string, AuditEventMessage>>();
            var producer = Producer();
            producer
                .Setup(p => p.ProduceAsync(It.IsAny<string>(),
                    It.IsAny<Message<string, AuditEventMessage>>(), It.IsAny<CancellationToken>()))
                .Returns(delivery.Task);
            var (publisher, metrics) = Build(producer, responseTimeout: TimeSpan.FromMilliseconds(50));

            await publisher.PublishAsync(AnEvent(), CancellationToken.None);
            Assert.Empty(metrics.Failures);

            //what librdkafka reports once message.timeout.ms passes with the broker still down.
            delivery.SetException(new KafkaException(ErrorCode.Local_MsgTimedOut));

            Assert.Equal("Facility", Assert.Single(metrics.Failures));
        }
    }
}
