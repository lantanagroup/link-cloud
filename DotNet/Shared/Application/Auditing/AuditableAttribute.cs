using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace LantanaGroup.Link.Shared.Application.Auditing
{
    /// <summary>
    /// Marks a controller action as auditable: one audit event per successful response, none when the
    /// response is an error or the action throws.
    /// </summary>
    /// <remarks>
    /// The whole of what most endpoints need. Action and resource come from the ADR's table for the
    /// service, a note only where that table specifies one; anything the attribute cannot know, the
    /// handler writes to <see cref="IAuditContext"/>. The minimal-API filter builds the same type by
    /// hand, so both paths hand the factory the same thing.
    /// </remarks>
    /// <example>
    /// <code>
    /// [Auditable(AuditEventType.Update, "Facility")]
    /// [Auditable(AuditEventType.SoftDelete, "Facility", RecordIdRouteKey = "facilityId")]
    /// [Auditable(AuditEventType.Update, "SftpConfiguration", FacilityIdRouteKey = "organizationId")]
    /// [Auditable(AuditEventType.Delete, "HSLOC", "All HSLOC codes for version {version} deleted")]
    /// [Auditable(AuditEventType.Update, "HSLOC", "HSLOC updated to version {version}", SuppressPropertyChanges = true)]
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class AuditableAttribute : Attribute, IFilterFactory
    {
        public AuditableAttribute(AuditEventType action, string resource, string? notes = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(resource);

            Action = action;
            Resource = resource;
            Notes = notes;
        }

        /// <summary>
        /// What the endpoint does, unless the handler overrides it through
        /// <see cref="IAuditContext.ActionOverride"/>.
        /// </summary>
        public AuditEventType Action { get; }

        /// <summary>
        /// What the endpoint acts on, as the ADR's service table names it.
        /// </summary>
        public string Resource { get; }

        /// <summary>
        /// The note template, given only for the endpoints whose ADR row specifies a note.
        /// Placeholders written as <c>{name}</c> are filled from the handler's context values first
        /// and then from the route.
        /// </summary>
        public string? Notes { get; }

        /// <summary>
        /// The route value holding the id of the affected record, for the endpoints that do not call
        /// it <c>id</c>. It must be the row's own id, never the facility it belongs to. The handler can
        /// override it through <see cref="IAuditContext.RecordId"/>.
        /// </summary>
        public string RecordIdRouteKey { get; set; } = "id";

        /// <summary>
        /// The route value holding the facility, for endpoints that do not call it
        /// <c>facilityId</c> - Data Acquisition's SFTP routes name it <c>organizationId</c>. Falls
        /// back to a query parameter of the same name, and the handler can override both through
        /// <see cref="IAuditContext.FacilityId"/>.
        /// </summary>
        public string FacilityIdRouteKey { get; set; } = "facilityId";

        /// <summary>
        /// Leaves property changes off this endpoint's events, for an Update that changes too many
        /// rows for them to be useful - Normalization's HSLOC upload - so its note says what changed
        /// instead. Use it only where the entries would tell a reader nothing.
        /// </summary>
        public bool SuppressPropertyChanges { get; set; }

        /// <summary>
        /// Never reusable: <see cref="AuditableFilter"/> carries what the action stage decided over to
        /// the result stage, so each request needs its own instance.
        /// </summary>
        public bool IsReusable => false;

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
            new AuditableFilter(this, serviceProvider.GetService<AuditRecorder>() ?? throw NotRegistered(this));

        /// <summary>
        /// The error for an auditable endpoint in a host that never called AddLinkAuditing - most
        /// often a hand-built test host. Without it the request fails with the container's own
        /// message, which names AuditRecorder but not the line that registers it.
        /// </summary>
        internal static InvalidOperationException NotRegistered(AuditableAttribute auditable) =>
            new($"{auditable.Action} {auditable.Resource} is marked auditable, but the shared audit mechanism is not " +
                "registered in this host. Call builder.Services.AddLinkAuditing<TContext>() with the context the " +
                "endpoint writes through, or AddLinkAuditing() for a service with no DbContext - in a test host " +
                "as well as in Program.cs.");
    }
}
