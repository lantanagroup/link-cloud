using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Shared.Application.Auditing
{
    /// <summary>
    /// Where the filter and the interceptor meet, one instance per request.
    /// </summary>
    /// <remarks>
    /// The filter knows the endpoint, the caller and the response; the interceptor knows which
    /// properties changed. Neither needs what the other knows: both write here, and the factory
    /// reads it once at the end of the request. A handler writes here only for what the endpoint
    /// cannot tell the filter by itself - a facility id from the body, which branch a
    /// create-or-update took, or a value a note needs.
    /// </remarks>
    public interface IAuditContext
    {
        /// <summary>
        /// Whether this scope is an audited request. Set by the audit filter when it begins; a
        /// handler leaves it alone.
        /// </summary>
        /// <remarks>
        /// Entity Framework runs the interceptor on every save in the host, listeners and jobs
        /// included, which no filter wraps. Until this is set it captures nothing.
        /// </remarks>
        bool IsRecording { get; set; }

        /// <summary>
        /// Set this when the facility the request concerns is not in the route - typically because it
        /// arrived in the request body.
        /// </summary>
        string? FacilityId { get; set; }

        /// <summary>
        /// The patient the request concerns, for the endpoints where one applies.
        /// </summary>
        string? PatientId { get; set; }

        /// <summary>
        /// The id that uniquely identifies the row the request acted on. Set by the handler whenever
        /// the route does not carry that row's own id - every create, and any endpoint whose id
        /// arrives in the body - and preferred over everything else.
        /// </summary>
        /// <remarks>
        /// Nothing guesses it from what was saved: that row may be a child or one of several, and a
        /// missing id is visible where a wrong one is not.
        /// </remarks>
        string? RecordId { get; set; }

        /// <summary>
        /// What the endpoint actually did, for an endpoint that creates on one branch and updates on
        /// another. Overrides the action on the attribute.
        /// </summary>
        AuditEventType? ActionOverride { get; set; }

        /// <summary>
        /// Drops the attribute's notes template for this request, for a note that only applies to one
        /// branch of the handler.
        /// </summary>
        bool SuppressNote { get; set; }

        /// <summary>
        /// Values a notes template can refer to by name, filled in by the handler. Names are matched
        /// without regard to case.
        /// </summary>
        IReadOnlyDictionary<string, object?> Values { get; }

        /// <summary>
        /// Records a value a notes template can refer to as <c>{name}</c>.
        /// </summary>
        void Set(string name, object? value);

        /// <summary>
        /// The old and new values the interceptor captured. A handler can add to this, but normally
        /// does not have to.
        /// </summary>
        List<PropertyChangeModel> PropertyChanges { get; }
    }

    /// <inheritdoc cref="IAuditContext"/>
    public sealed class AuditContext : IAuditContext
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);

        public bool IsRecording { get; set; }

        public string? FacilityId { get; set; }

        public string? PatientId { get; set; }

        public string? RecordId { get; set; }

        public AuditEventType? ActionOverride { get; set; }

        public bool SuppressNote { get; set; }

        public IReadOnlyDictionary<string, object?> Values => _values;

        public void Set(string name, object? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            _values[name] = value;
        }

        public List<PropertyChangeModel> PropertyChanges { get; } = [];
    }
}
