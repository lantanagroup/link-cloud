using LantanaGroup.Link.Shared.Application.Models.Retention;

namespace LantanaGroup.Link.Report.Application.Interfaces;

/// <summary>
/// Publishes a retention cleanup request to the participants.
/// </summary>
public interface IRetentionCheckRequestedPublisher
{
    /// <summary>
    /// Publishes <paramref name="request"/> on <c>RetentionCheckRequested</c> and returns once the broker has
    /// accepted it. Throws when it was not delivered.
    /// </summary>
    Task PublishAsync(RetentionCheckRequestedValue request, CancellationToken cancellationToken);
}
