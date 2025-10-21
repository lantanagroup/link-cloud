namespace LantanaGroup.Link.Shared.Application.Utilities
{
    public class StringValueAttribute : Attribute
    {
        public string StringValue { get; set; }

        public StringValueAttribute(string value)
        {
            StringValue = value;
        }
    }

    /// <summary>
    /// Marks a <c>RequestStatus</c> value as terminal: a log in this status will not
    /// transition further. Drives tail-completion and completion queries. Consume it via
    /// <c>RequestStatusExtensions.TerminalStatuses</c> / <c>IsTerminal</c> rather than
    /// re-listing statuses inline.
    /// <para>
    /// This is a distinct concept from <c>CancellableStatusAttribute</c>: "finished" and
    /// "eligible for cancellation" are independent axes that happen to be complementary today
    /// but are modelled separately so they can diverge without silent coupling.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TerminalStatusAttribute : Attribute;

    /// <summary>
    /// Marks a <c>RequestStatus</c> value as eligible for cancellation: a bulk-cancel may
    /// transition a log in this (still in-flight) status to <c>Cancelled</c>. Consume it via
    /// <c>RequestStatusExtensions.CancellableStatuses</c> / <c>IsCancellable</c>.
    /// <para>
    /// Deliberately separate from <c>TerminalStatusAttribute</c> — a terminal log is "finished"
    /// and so is not cancellable, but the two sets are tracked independently rather than one
    /// being defined as the negation of the other.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class CancellableStatusAttribute : Attribute;
}
