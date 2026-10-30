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
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILogger<AuditRecorder> _logger;

        public AuditRecorder(IAuditContext audit, IAuditEventFactory factory, IAuditPublisher publisher,
            IHostApplicationLifetime lifetime, ILogger<AuditRecorder> logger)
        {
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
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
                _logger.LogError(ex, "Audit event could not be built for {Action} {Resource}.",
                    auditable.Action, auditable.Resource);
                return;
            }

            await _publisher.PublishAsync(auditEvent, CompletionToken);
        }

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
