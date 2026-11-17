using System.Collections;

namespace LantanaGroup.Link.Shared.Application.Models.Configs
{
    /// <summary>
    /// Which values an audit event's property changes mask. Every entry here is a value a service
    /// must not publish to Kafka: authentication, credentials and secrets by name or pattern, and
    /// anything a developer marked <c>[AuditMask]</c> on the entity - which is how personal data
    /// is kept out.
    /// </summary>
    /// <remarks>
    /// A service adds to these lists at registration and can never take an entry away: the shared
    /// defaults are the floor, not a suggestion. A changed value that any of them catches is masked,
    /// not omitted: the event names the property with <see cref="MaskedValue"/> on both sides, so a
    /// reader of the audit trail can see that it changed, and when, but never from what or to what.
    /// </remarks>
    public sealed class AuditOptions
    {
        /// <summary>
        /// What a masked property change carries as both its old and its new value. It is the same
        /// on both sides, so it says nothing about whether a value was set, changed or cleared.
        /// </summary>
        public const string MaskedValue = "[redacted]";

        /// <summary>
        /// How hard the direct publisher tries before giving up on an event.
        /// </summary>
        public AuditPublishOptions Publish { get; } = new();

        /// <summary>
        /// Property names whose values are masked, matched whole and without regard to case.
        /// </summary>
        public ICollection<string> MaskedPropertyNames { get; } = new AddOnlyStringSet
        {
            "Password", "Secret", "ClientSecret", "Token", "AccessToken", "RefreshToken",
            "ApiKey", "PrivateKey", "Passphrase", "ConnectionString", "Authentication", "Credentials"
        };

        /// <summary>
        /// Modify timestamps, which a service's UpdateBaseEntityInterceptor writes on every save and
        /// so are not a change anyone made: the event's EventDate already says when it happened.
        /// Matched whole and without regard to case. These are noise rather than secrets, so they
        /// are kept apart from <see cref="MaskedPropertyNames"/>; a service whose timestamp has a
        /// name not listed here adds it.
        /// </summary>
        public ICollection<string> TimestampPropertyNames { get; } = new AddOnlyStringSet
        {
            "ModifyDate", "ModifiedDate", "ModifiedOn", "LastModifiedOn", "UpdatedAt", "LastUpdatedDate", "LastUpdated"
        };

        /// <summary>
        /// Wildcard patterns matched against property names, for the names nobody listed; a match is
        /// masked. <c>*</c> and <c>?</c> are wildcards; matching ignores case.
        /// </summary>
        public ICollection<string> MaskedPropertyPatterns { get; } = new AddOnlyStringSet
        {
            "*Secret*", "*Password*", "*Token*", "*Key"
        };
    }

    /// <summary>
    /// A set of names a service can add to but never remove from, so one service cannot quietly
    /// drop a masking entry every other service relies on. Comparison ignores case, because an entity
    /// property is as likely to be <c>clientSecret</c> as <c>ClientSecret</c>.
    /// </summary>
    public sealed class AddOnlyStringSet : ICollection<string>
    {
        private readonly HashSet<string> _items = new(StringComparer.OrdinalIgnoreCase);

        public int Count => _items.Count;

        public bool IsReadOnly => false;

        public void Add(string item)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(item);
            _items.Add(item);
        }

        public bool Contains(string item) => item is not null && _items.Contains(item);

        public void CopyTo(string[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

        public IEnumerator<string> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Remove(string item) =>
            throw new NotSupportedException(
                "Audit masking entries are shared across every service and cannot be removed. " +
                "Add entries through AddLinkAuditing options or [AuditMask] on the entity.");

        public void Clear() =>
            throw new NotSupportedException(
                "Audit masking entries are shared across every service and cannot be cleared. " +
                "Add entries through AddLinkAuditing options or [AuditMask] on the entity.");
    }
}
