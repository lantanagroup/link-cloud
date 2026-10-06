using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Shared.Application.Services.ResourceCache
{
    /// <summary>
    /// One attempt to read the cache.
    /// </summary>
    /// <remarks>
    /// An unreachable cache is distinct from an absent key. Both read through to durable storage, but
    /// only one of them is the cache being in trouble, and a reader that sees an empty list for both
    /// cannot say which happened.
    /// </remarks>
    /// <param name="Resources">What the cache held, empty when it held nothing or could not be read.</param>
    /// <param name="Unavailable">Whether the read failed rather than finding nothing.</param>
    internal readonly record struct CacheRead(List<DomainResource> Resources, bool Unavailable);
}
