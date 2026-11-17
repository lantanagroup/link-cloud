using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Shared.Application.Auditing
{
    /// <summary>
    /// How an assembled audit event leaves the service.
    /// </summary>
    /// <remarks>
    /// One method, so that the transactional outbox feature can replace the implementation without
    /// touching a filter, a handler or a service's registration. The direct publisher this story
    /// ships sends to Kafka after the handler's transaction commits; an outbox publisher will enqueue
    /// on the request's DbContext before it commits, at which point audit events stop being losable.
    /// </remarks>
    public interface IAuditPublisher
    {
        /// <summary>
        /// Sends the event, or does everything it can short of failing the request.
        /// </summary>
        Task PublishAsync(AuditEventMessage message, CancellationToken cancellationToken);
    }
}
