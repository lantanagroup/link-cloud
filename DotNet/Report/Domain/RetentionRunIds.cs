using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.Report.Domain;

/// <summary>
/// The id of one scheduled retention run, derived from the firing it ran for. A run recovered on another
/// Report instance gets the same id as the run it replaces, so their cleanups group together in the cleanup
/// history.
/// </summary>
/// <remarks>
/// The id only groups cleanups. It is predictable and must never decide access; nothing about a cleanup's
/// safety depends on it, because the cleanup manager claims each report only once.
/// </remarks>
public static class RetentionRunIds
{
    // Fixed forever: changing it would stop recovered runs grouping with the runs they replace.
    private static readonly Guid Namespace = new("0dac4c7a-dbd5-4771-98fe-aae1bccbd68e");

    /// <summary>
    /// The run id for the firing due at <paramref name="scheduledFireTime"/>, whatever offset it carries.
    /// </summary>
    public static Guid For(DateTimeOffset scheduledFireTime)
    {
        return NameBasedGuid.CreateVersion5(Namespace, $"retention-check|{scheduledFireTime.UtcDateTime:O}");
    }
}
