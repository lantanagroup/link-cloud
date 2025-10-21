namespace LantanaGroup.Link.Terminology.Application.Exceptions;

/// <summary>
/// Thrown when an operation names a code group that is not present in the cache.
/// </summary>
/// <remarks>
/// Distinct from a bare <see cref="KeyNotFoundException"/> so that callers can map "the caller asked for
/// something that is not cached" (a 404) without also catching the dictionary lookups that fail inside the
/// cache's own loading code (a defect, which must surface as a 500 with a traceId rather than being
/// disguised as a not-found answer). It derives from <see cref="KeyNotFoundException"/> so the narrower
/// contract stays source-compatible with any caller still catching the base type.
/// </remarks>
public class CodeGroupNotFoundException : KeyNotFoundException
{
    /// <summary>
    /// Creates the exception with a message describing the code group that could not be found.
    /// </summary>
    public CodeGroupNotFoundException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates the exception with a message and the underlying cause.
    /// </summary>
    public CodeGroupNotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
