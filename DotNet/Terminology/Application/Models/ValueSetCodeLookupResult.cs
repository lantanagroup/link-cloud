namespace LantanaGroup.Link.Terminology.Application.Models;

/// <summary>
/// The response of the cached ValueSet code lookup: one member of a cached value set, reported with both
/// the status the value set itself declares and the status that will actually be applied.
/// </summary>
/// <remarks>
/// The two statuses are separate because they answer different questions, and conflating them is what
/// LEGLINK-889 was raised over. A value set may declare its own membership status (the optional fourth CSV
/// column added by LEGLINK-639), in which case it overrides the code system; a value set with no status
/// column declares nothing and its members inherit whatever the CodeSystem says. Reporting only one number
/// leaves a caller unable to tell "this value set says inactive" from "this value set is silent and the
/// code system says inactive" — and unable to tell either from a code system edit that never took effect.
/// </remarks>
public class ValueSetCodeLookupResult
{
    /// <summary>
    /// The code system URI the member was found under. A value set groups its members by system, so this
    /// names which of them matched — it is the system the effective status was rejoined from.
    /// </summary>
    public required string System { get; set; }

    /// <summary>
    /// The code value.
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// The human-readable display text the value set carries for the code.
    /// </summary>
    public required string Display { get; set; }

    /// <summary>
    /// The status the value set declares for this member, or <c>null</c> when the value set's CSV has no
    /// status column and therefore declares nothing. Null is not the same as Active: it means the question
    /// is deferred to the code system.
    /// </summary>
    public CodeStatus? MembershipStatus { get; set; }

    /// <summary>
    /// The status that applies — the declared membership status when there is one, otherwise the status
    /// rejoined from the cached CodeSystem for <see cref="System"/>. This is the value
    /// <c>ValueSet/$validate-code</c> acts on, so the two always agree.
    /// </summary>
    public required CodeStatus EffectiveStatus { get; set; }
}
