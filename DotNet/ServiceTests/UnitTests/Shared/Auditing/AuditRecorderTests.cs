using System.Security.Claims;
using LantanaGroup.Link.Shared.Application.Auditing;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared.Auditing
{
    /// <summary>
    /// The recorder's last line of defence: by the time either filter calls it the handler's change
    /// is committed and the response may already be written, so nothing it does to record that change
    /// is allowed to turn it into an error the caller is told about.
    /// </summary>
    /// <remarks>
    /// The publisher this story ships accounts for its own failures and returns, so these cover a
    /// publisher that breaks that contract: a service's own implementation, the outbox one that
    /// replaces it, or a retry configuration its loop cannot work with.
    /// </remarks>
    [Trait("Category", "UnitTests")]
    public class AuditRecorderTests
    {
        private static readonly AuditableAttribute UpdateFacility = new(AuditEventType.Update, "Facility");

        private readonly FakeAuditMetrics _metrics = new();
        private readonly CapturingLogger<AuditRecorder> _logger = new();

        private Task Record(IAuditPublisher publisher, IAuditEventFactory? factory = null) =>
            AuditTest.Recorder(new AuditContext(), publisher, factory, _metrics, _logger)
                .RecordAsync(UpdateFacility, AuditTest.SignedInAs("nurse@example.org"),
                    new RouteValueDictionary { ["id"] = "f1e2d3c4" });

        [Fact]
        public async Task APublisherThatThrows_DoesNotFailTheRequest()
        {
            var publisher = new ThrowingAuditPublisher(new InvalidOperationException("the producer is disposed"));

            //no assertion needed beyond this returning: an exception here is the caller's error page.
            await Record(publisher);
        }

        [Fact]
        public async Task APublisherThatThrows_CountsTheLostEventAndLogsIt()
        {
            await Record(new ThrowingAuditPublisher(new InvalidOperationException("the producer is disposed")));

            //the counter every service story's QA actions watch, so a publisher failing outright is as
            //visible as one the broker never confirmed.
            Assert.Equal(UpdateFacility.Resource, Assert.Single(_metrics.Failures));

            var (level, message) = Assert.Single(_logger.Entries);
            Assert.Equal(LogLevel.Error, level);
            Assert.Contains(UpdateFacility.Resource, message);
            Assert.Contains("already committed", message);
        }

        [Fact]
        public async Task APublisherThatThrowsSynchronously_IsContainedToo()
        {
            //an implementation that validates its argument before the first await throws on the way in
            //rather than returning a faulted task, which a try/catch only covers if it wraps the call
            //itself.
            await Record(new ThrowingAuditPublisher(new ArgumentException("no topic configured"),
                synchronously: true));

            Assert.Single(_metrics.Failures);
            Assert.Equal(LogLevel.Error, Assert.Single(_logger.Entries).Level);
        }

        [Fact]
        public async Task AFactoryThatThrows_PublishesNothingAndCountsTheLostEvent()
        {
            var publisher = new FakeAuditPublisher();

            await Record(publisher, new ThrowingAuditEventFactory());

            Assert.Empty(publisher.Published);
            Assert.Equal(UpdateFacility.Resource, Assert.Single(_metrics.Failures));
            Assert.Equal(LogLevel.Error, Assert.Single(_logger.Entries).Level);
        }

        [Fact]
        public async Task APublisherThatSucceeds_CountsNothingAndLogsNothing()
        {
            var publisher = new FakeAuditPublisher();

            await Record(publisher);

            Assert.Single(publisher.Published);
            Assert.Empty(_metrics.Failures);
            Assert.Empty(_logger.Entries);
        }

        private sealed class ThrowingAuditPublisher(Exception failure, bool synchronously = false) : IAuditPublisher
        {
            public Task PublishAsync(AuditEventMessage message, CancellationToken cancellationToken)
            {
                if (synchronously)
                {
                    throw failure;
                }

                return Task.FromException(failure);
            }
        }

        private sealed class ThrowingAuditEventFactory : IAuditEventFactory
        {
            public AuditEventMessage Create(AuditableAttribute auditable, ClaimsPrincipal? principal,
                RouteValueDictionary? route, IAuditContext audit, IQueryCollection? query = null) =>
                throw new FormatException("the note template names a placeholder nothing fills");
        }
    }
}
