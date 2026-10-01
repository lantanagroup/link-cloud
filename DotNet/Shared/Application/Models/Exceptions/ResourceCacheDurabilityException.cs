namespace LantanaGroup.Link.Shared.Application.Models.Exceptions;

/// <summary>
/// Thrown when a resource cache entry could not be written to durable storage, so the cache keys it
/// covers must not be advertised to any downstream consumer.
/// </summary>
/// <remarks>
/// Raised by the durability barrier rather than by the write itself: the background writer records
/// the failure, and the next caller waiting on those keys is the one that has to act on it. A caller
/// that catches this must not mark its work terminal or produce an event naming these keys, because
/// the cached copy can be evicted at any time and nothing would be left behind it.
/// </remarks>
public class ResourceCacheDurabilityException : Exception
{
    /// <summary>
    /// Creates an exception with no detail.
    /// </summary>
    public ResourceCacheDurabilityException()
    {
    }

    /// <summary>
    /// Creates an exception describing which keys failed and why.
    /// </summary>
    public ResourceCacheDurabilityException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates an exception wrapping the storage failure that caused it.
    /// </summary>
    public ResourceCacheDurabilityException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
