using System.Security.Claims;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.Shared.Application.Auditing
{
    /// <summary>
    /// Everything the two filters do identically: mark the request as audited, choose the governing
    /// cancellation token, assemble the event and publish it. Each filter only translates its own
    /// framework's idea of a request that succeeded, and the route values it exposes.
    /// </summary>
    public sealed class AuditRecorder
    {
        private readonly IAuditContext _audit;
        private readonly IAuditEventFactory _factory;
        private readonly IAuditPublisher _publisher;
        private readonly IAuditMetrics _metrics;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILogger<AuditRecorder> _logger;

        public AuditRecorder(IAuditContext audit, IAuditEventFactory factory, IAuditPublisher publisher,
            IAuditMetrics metrics, IHostApplicationLifetime lifetime, ILogger<AuditRecorder> logger)
        {
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Marks the scope as an audited request, so the interceptor captures what the action saves.
        /// What the action does to the database is its own business; the filter only decides whether
        /// it succeeded.
        /// </summary>
        public void Begin() => _audit.IsRecording = true;

        /// <summary>
        /// Builds and publishes the event for an action that has already taken effect.
        /// </summary>
        /// <remarks>
        /// Nothing in here is allowed to throw. By the time a filter calls this the handler's change
        /// is committed and, in MVC's result stage, the response is already written, so an exception
        /// escaping would turn a change that really happened into an error the caller is told about.
        /// Both failure paths count the lost event and log it, the same accounting
        /// <see cref="KafkaAuditPublisher"/> does for one the broker never confirmed.
        /// </remarks>
        public async Task RecordAsync(AuditableAttribute auditable, ClaimsPrincipal? user,
            RouteValueDictionary? routeValues, IQueryCollection? query = null)
        {
            AuditEventMessage auditEvent;
            try
            {
                auditEvent = _factory.Create(auditable, user, routeValues, _audit, query);
            }
            catch (Exception ex)
            {
                //the change is already committed; a bad note template or a missing claim must not
                //turn a successful request into an error.
                Lost(auditable);
                _logger.LogError(ex, "Audit event could not be built for {Action} {Resource}; " +
                    "the change it describes is already committed.", auditable.Action, auditable.Resource);
                return;
            }

            try
            {
                await _publisher.PublishAsync(auditEvent, CompletionToken);
            }
            catch (Exception ex)
            {
                //the publisher this story ships accounts for its own failures and returns, so getting
                //here means one broke that contract - the outbox implementation, a service's own, or a
                //retry configuration the loop cannot work with. The request has already succeeded, so
                //the event is written off rather than thrown. The publisher logs what it can about the
                //event itself; this records that it is gone.
                Lost(auditable);
                _logger.LogError(ex, "Audit event for {Action} {Resource} was not published and the publisher " +
                    "failed outright; the change it describes is already committed.",
                    auditable.Action, auditable.Resource);
            }
        }

        /// <summary>
        /// Counts an event that will never reach Kafka on the same meter as an abandoned publish, so
        /// one counter still answers how many audited changes went unrecorded.
        /// </summary>
        private void Lost(AuditableAttribute auditable) => _metrics.PublishFailed(auditable.Resource);

        /// <summary>
        /// What governs finishing the request, as opposed to running it.
        /// </summary>
        /// <remarks>
        /// Deliberately not HttpContext.RequestAborted: the change is already made, so a caller that
        /// hangs up must not decide whether it is described. Only shutdown interrupts this, and an
        /// event lost that way is counted and logged.
        /// </remarks>
        private CancellationToken CompletionToken => _lifetime.ApplicationStopping;
    }
}
