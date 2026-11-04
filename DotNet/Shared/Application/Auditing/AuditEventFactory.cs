using System.Security.Claims;
using System.Text.RegularExpressions;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.Shared.Application.Auditing
{
    /// <summary>
    /// Assembles the audit event from the things that know about the request: the endpoint's
    /// attribute, the caller's claims, the route and query string, and whatever the handler and the
    /// interceptor left in the context.
    /// </summary>
    public interface IAuditEventFactory
    {
        AuditEventMessage Create(AuditableAttribute auditable, ClaimsPrincipal? principal,
            RouteValueDictionary? route, IAuditContext audit, IQueryCollection? query = null);
    }

    /// <inheritdoc cref="IAuditEventFactory"/>
    public sealed class AuditEventFactory : IAuditEventFactory
    {
        private static readonly Regex Placeholder = new(@"\{(\w+)\}", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        private readonly ServiceInformation _serviceInformation;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<AuditEventFactory> _logger;

        public AuditEventFactory(ServiceInformation serviceInformation, TimeProvider timeProvider,
            ILogger<AuditEventFactory> logger)
        {
            _serviceInformation = serviceInformation ?? throw new ArgumentNullException(nameof(serviceInformation));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public AuditEventMessage Create(AuditableAttribute auditable, ClaimsPrincipal? principal,
            RouteValueDictionary? route, IAuditContext audit, IQueryCollection? query = null)
        {
            ArgumentNullException.ThrowIfNull(auditable);
            ArgumentNullException.ThrowIfNull(audit);

            route ??= [];
            var action = audit.ActionOverride ?? auditable.Action;

            return new AuditEventMessage
            {
                FacilityId = ResolveFacilityId(auditable, route, query, audit),
                ServiceName = ServiceName(),
                EventDate = _timeProvider.GetUtcNow().UtcDateTime,
                Action = action,
                Resource = auditable.Resource,
                ResourceId = ResolveResourceId(auditable, route, audit),
                PatientId = audit.PatientId,
                User = ResolveActor(principal),

                //no identifier until the NHSN app's users are reconciled with Account's, and no
                //correlation id: a REST request is not part of a pipeline run.
                UserId = null,
                CorrelationId = null,

                PropertyChanges = ResolvePropertyChanges(action, auditable, audit),
                Notes = audit.SuppressNote ? null : FillTemplate(auditable.Notes, route, audit.Values)
            };
        }

        /// <summary>
        /// What the interceptor found, on an Update whose endpoint does not suppress them.
        /// </summary>
        /// <remarks>
        /// The ADR recommends them on Update and says nothing of the others; leaving them off the
        /// others is this mechanism's choice, on the size grounds the ADR gives for leaving out
        /// notes. A soft delete is saved as a modification, so the interceptor finds
        /// <c>IsDeleted</c> changed on every row - which the action already says, and on a
        /// facility-wide endpoint could pass Kafka's size limit. The action counted is the one the
        /// event reports, so an <c>ActionOverride</c> to Create drops them too.
        /// <para>
        /// <see cref="AuditableAttribute.SuppressPropertyChanges"/> is the endpoint's opt-out, for
        /// a request that changes too many rows for the entries to be useful.
        /// </para>
        /// </remarks>
        private static List<PropertyChangeModel>? ResolvePropertyChanges(AuditEventType action,
            AuditableAttribute auditable, IAuditContext audit) =>
            action == AuditEventType.Update && !auditable.SuppressPropertyChanges && audit.PropertyChanges.Count > 0
                ? audit.PropertyChanges
                : null;

        /// <summary>
        /// The short name the service is configured under, which is what the existing audit senders
        /// record and what the Audit UI filters on. ServiceName is the display name
        /// ("Link Tenant Service") and only stands in if no config name was set.
        /// </summary>
        private string ServiceName() =>
            string.IsNullOrWhiteSpace(_serviceInformation.ServiceConfigName)
                ? _serviceInformation.ServiceName
                : _serviceInformation.ServiceConfigName;

        /// <summary>
        /// The caller's email claim, or nothing at all.
        /// </summary>
        /// <remarks>
        /// No placeholder is invented: a null User says truthfully that the caller is unknown, where
        /// "SystemAccount" would claim something the token does not support. A token from
        /// CreateSystemToken does carry an email - the configured Link admin address - so those
        /// calls record it until LEGLINK-1359 decides whether un-attributed calls should omit it.
        /// </remarks>
        private static string? ResolveActor(ClaimsPrincipal? principal)
        {
            var email = principal?.FindFirst(LinkAuthorizationConstants.LinkSystemClaims.Email)?.Value;
            return string.IsNullOrWhiteSpace(email) ? null : email;
        }

        /// <summary>
        /// What the handler said, then the route key the attribute names.
        /// </summary>
        /// <remarks>
        /// Either way it must uniquely identify the row acted on, so a route key names that row's
        /// own id, never the facility. Nothing falls back to a row the interceptor saw saved: that
        /// may be a child or one of several, and a wrong id looks exactly like a right one. A
        /// missing id is not an error - a bulk endpoint has no single record - but a single-record
        /// endpoint naming the wrong route key loses it silently, hence the Debug line.
        /// </remarks>
        private string? ResolveResourceId(AuditableAttribute auditable, RouteValueDictionary route, IAuditContext audit)
        {
            if (!string.IsNullOrWhiteSpace(audit.RecordId))
            {
                return audit.RecordId;
            }

            var fromRoute = RouteValue(route, auditable.RecordIdRouteKey);
            if (fromRoute is not null)
            {
                return fromRoute;
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Audit event for {Action} {Resource} has no ResourceId: route key '{RouteKey}' is not present and the handler set none. Route keys: {RouteKeys}",
                    auditable.Action, auditable.Resource, auditable.RecordIdRouteKey, string.Join(", ", route.Keys));
            }

            return null;
        }

        /// <summary>
        /// What the handler said, then the route value the attribute names, then a query string
        /// parameter of that name.
        /// </summary>
        /// <remarks>
        /// A repeated parameter (<c>?facilityId=a&amp;facilityId=b</c>) names no single facility, so
        /// it is not guessed at, and a query value is caller input no route constraint has shaped,
        /// so it is sanitised. Many endpoints concern no facility, so a missing one is not an error
        /// - but a route naming it differently without the attribute saying so loses it silently,
        /// hence the Debug line.
        /// </remarks>
        private string? ResolveFacilityId(AuditableAttribute auditable, RouteValueDictionary route,
            IQueryCollection? query, IAuditContext audit)
        {
            if (!string.IsNullOrWhiteSpace(audit.FacilityId))
            {
                return audit.FacilityId;
            }

            var fromRoute = RouteValue(route, auditable.FacilityIdRouteKey);
            if (fromRoute is not null)
            {
                return fromRoute;
            }

            string? sanitised = null;
            if (query is not null && query.TryGetValue(auditable.FacilityIdRouteKey, out var values) && values.Count == 1)
            {
                sanitised = values[0]?.SanitizeAndRemove();
            }

            if (!string.IsNullOrWhiteSpace(sanitised))
            {
                return sanitised;
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Audit event for {Action} {Resource} has no FacilityId: neither the route nor the query string carries one value for '{RouteKey}' and the handler set none. Route keys: {RouteKeys}",
                    auditable.Action, auditable.Resource, auditable.FacilityIdRouteKey, string.Join(", ", route.Keys));
            }

            return null;
        }

        private static string? RouteValue(RouteValueDictionary route, string key)
        {
            if (!route.TryGetValue(key, out var value))
            {
                return null;
            }

            var text = value?.ToString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        /// <summary>
        /// Fills a note template from the handler's values first, then the route. An unknown
        /// placeholder is left as it was written, so a note that was meant to carry a value says so
        /// in the audit trail instead of quietly reading as prose.
        /// </summary>
        private static string? FillTemplate(string? template, RouteValueDictionary route,
            IReadOnlyDictionary<string, object?> values)
        {
            if (string.IsNullOrEmpty(template))
            {
                return null;
            }

            return Placeholder.Replace(template, match =>
            {
                var key = match.Groups[1].Value;

                if (values.TryGetValue(key, out var value))
                {
                    return value?.ToString() ?? string.Empty;
                }

                if (route.TryGetValue(key, out var routeValue))
                {
                    return routeValue?.ToString() ?? string.Empty;
                }

                return match.Value;
            });
        }
    }
}
