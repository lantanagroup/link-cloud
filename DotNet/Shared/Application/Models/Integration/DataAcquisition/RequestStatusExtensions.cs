using System.Reflection;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;

/// <summary>
/// Helpers for <see cref="RequestStatus"/> terminality, driven by the
/// <see cref="TerminalStatusAttribute"/> markers on the enum so the terminal set is
/// defined in exactly one place.
/// </summary>
public static class RequestStatusExtensions
{
    // Reflected once at type-load from the marker attributes on RequestStatus.
    private static readonly HashSet<RequestStatus> TerminalSet = StatusesMarkedWith<TerminalStatusAttribute>();
    private static readonly HashSet<RequestStatus> CancellableSet = StatusesMarkedWith<CancellableStatusAttribute>();

    private static HashSet<RequestStatus> StatusesMarkedWith<TAttribute>() where TAttribute : Attribute =>
        Enum.GetValues<RequestStatus>()
            .Where(status => typeof(RequestStatus)
                .GetField(status.ToString())
                ?.GetCustomAttribute<TAttribute>() is not null)
            .ToHashSet();

    /// <summary>
    /// The terminal statuses (a log in one of these will not transition further), materialized
    /// as an array so it can be used inside EF Core query predicates (translated to a SQL
    /// <c>IN</c>). Use this in queries; use <see cref="IsTerminal"/> for in-memory checks.
    /// </summary>
    public static readonly RequestStatus[] TerminalStatuses = TerminalSet.ToArray();

    /// <summary>
    /// The statuses eligible for cancellation (still in-flight). Materialized as an array for
    /// use inside EF Core query predicates. Use this in queries; use <see cref="IsCancellable"/>
    /// for in-memory checks. Tracked independently of <see cref="TerminalStatuses"/>.
    /// </summary>
    public static readonly RequestStatus[] CancellableStatuses = CancellableSet.ToArray();

    /// <summary>
    /// True if the status is terminal. For in-memory checks only — EF Core cannot translate
    /// this method to SQL, so use <see cref="TerminalStatuses"/> inside query predicates.
    /// </summary>
    public static bool IsTerminal(this RequestStatus status) => TerminalSet.Contains(status);

    /// <summary>
    /// True if the status is eligible for cancellation. For in-memory checks only — EF Core
    /// cannot translate this method to SQL, so use <see cref="CancellableStatuses"/> inside
    /// query predicates.
    /// </summary>
    public static bool IsCancellable(this RequestStatus status) => CancellableSet.Contains(status);
}
