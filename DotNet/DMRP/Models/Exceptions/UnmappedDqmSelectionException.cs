namespace LantanaGroup.Link.DMRP.Models.Exceptions;

/// <summary>
/// A facility save selected a dQM at a frequency that no measure mapping covers, so there is no NHSN
/// measure to enroll the facility in on the Mock DMRP API.
/// </summary>
/// <remarks>
/// An <see cref="ApplicationException"/> so the facility endpoints answer 400 with this message, the way
/// they already do for the host's own validation failures.
/// </remarks>
public sealed class UnmappedDqmSelectionException : ApplicationException
{
    /// <summary>
    /// Creates the exception, naming every unmapped selection.
    /// </summary>
    public UnmappedDqmSelectionException(IEnumerable<string> unmapped)
        : base("No measure mapping covers these selected reports, so they cannot be enrolled in the Mock DMRP " +
               $"API: {string.Join(", ", unmapped)}. Add a measure mapping for each, or remove it from the facility.")
    {
    }
}
