using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LantanaGroup.Link.Shared.Application.Auditing
{
    /// <summary>
    /// The minimal-API half of the mechanism, for Account, the BFFs and any new service that maps
    /// endpoints instead of writing controllers.
    /// </summary>
    /// <remarks>
    /// Same core as <see cref="AuditableFilter"/>, down to the publisher; only the hook into .NET and
    /// the test for success differ. The filter instance is reused across requests, so the per-request
    /// services are resolved from the request on each invocation.
    /// <para>
    /// Both filters publish only once the handler's result has executed, because a minimal API, like
    /// MVC, executes that result after the filter has returned. Where MVC gives a result stage to
    /// hook, this returns a result of its own - see <c>AuditedResult</c> - that executes the
    /// handler's and records against the status it set. A handler returning a plain value, or
    /// nothing, rather than an <see cref="IResult"/> is the one case left recorded on the way out.
    /// </para>
    /// </remarks>
    public sealed class AuditableEndpointFilter : IEndpointFilter
    {
        private readonly AuditableAttribute _auditable;

        public AuditableEndpointFilter(AuditEventType action, string resource, string? notes = null,
            string recordIdRouteKey = "id", string facilityIdRouteKey = "facilityId",
            bool suppressPropertyChanges = false)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(recordIdRouteKey);
            ArgumentException.ThrowIfNullOrWhiteSpace(facilityIdRouteKey);

            _auditable = new AuditableAttribute(action, resource, notes)
            {
                RecordIdRouteKey = recordIdRouteKey,
                FacilityIdRouteKey = facilityIdRouteKey,
                SuppressPropertyChanges = suppressPropertyChanges
            };
        }

        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(next);

            var httpContext = context.HttpContext;
            var recorder = httpContext.RequestServices.GetService<AuditRecorder>()
                ?? throw AuditableAttribute.NotRegistered(_auditable);

            recorder.Begin();

            //a handler that throws produces no result to execute: no event describes an action that
            //did not succeed.
            var result = await next(context);

            if (!IsSuccess(result))
            {
                return result;
            }

            if (result is IResult handlerResult)
            {
                return new AuditedResult(handlerResult, _auditable, recorder);
            }

            //a plain value, or nothing at all, is turned into a response by the request delegate
            //itself, in a way nothing here can reproduce faithfully - a string goes out as text
            //rather than JSON, and the serialiser options are the host's - so those keep the older
            //behaviour of being recorded on the way out. No audited endpoint returns either today;
            //one that did would carry the risk this filter closes for every other result.
            await recorder.RecordAsync(_auditable, httpContext.User,
                httpContext.Request.RouteValues, httpContext.Request.Query);
            return result;
        }

        /// <summary>
        /// Whether the handler's return value is a success.
        /// </summary>
        /// <remarks>
        /// A minimal-API handler lets exceptions out rather than packaging them, so returning at all
        /// covers failure. What is left is a result that names its own status code - everything from
        /// Results and TypedResults does - and a plain value, which becomes a 200. Either way the
        /// status the result really wrote is checked again once it has executed.
        /// </remarks>
        private static bool IsSuccess(object? result) => result switch
        {
            IStatusCodeHttpResult { StatusCode: { } code } => IsSuccess(code),
            _ => true
        };

        private static bool IsSuccess(int statusCode) => statusCode is >= 200 and < 300;

        /// <summary>
        /// The handler's own result, with the audit event published once executing it has succeeded.
        /// </summary>
        /// <remarks>
        /// The reason this filter returns a result of its own. A minimal API executes the handler's
        /// result after the whole filter pipeline has unwound, so the filter never sees it executed -
        /// recording on the way out would describe a response that still had serialisation ahead of
        /// it, and a result that then failed to execute would leave a success event behind a failed
        /// response. Executing the handler's result here puts the event where the status it set is
        /// known, which is what <see cref="AuditableFilter"/>'s result stage does for MVC.
        /// <para>
        /// The cost is that what the pipeline sees after this filter is the wrapper, not the
        /// handler's own result, and the wrapper carries none of the result interfaces the handler's
        /// did - <see cref="IStatusCodeHttpResult"/> among them. Nothing looks at it today: the
        /// endpoints that pair this with another filter put that filter outside, and it passes the
        /// result straight through. A filter registered outside <c>Auditable</c> that does inspect
        /// the result by interface would see an opaque <see cref="IResult"/>.
        /// </para>
        /// </remarks>
        private sealed class AuditedResult : IResult
        {
            private readonly IResult _inner;
            private readonly AuditableAttribute _auditable;
            private readonly AuditRecorder _recorder;

            public AuditedResult(IResult inner, AuditableAttribute auditable, AuditRecorder recorder)
            {
                _inner = inner;
                _auditable = auditable;
                _recorder = recorder;
            }

            public async Task ExecuteAsync(HttpContext httpContext)
            {
                ArgumentNullException.ThrowIfNull(httpContext);

                //an exception on the way out leaves no event behind, whatever status was already
                //set: the caller's response is broken, and nothing may claim otherwise.
                await _inner.ExecuteAsync(httpContext);

                if (!IsSuccess(httpContext.Response.StatusCode))
                {
                    return;
                }

                await _recorder.RecordAsync(_auditable, httpContext.User,
                    httpContext.Request.RouteValues, httpContext.Request.Query);
            }
        }
    }

    /// <summary>
    /// Marks a mapped endpoint as auditable, the minimal-API counterpart of
    /// <see cref="AuditableAttribute"/>.
    /// </summary>
    public static class AuditableEndpointExtensions
    {
        /// <example>
        /// <code>
        /// userEndpoints.MapDelete("/user/{id}", DeleteExistingUser.Handle)
        ///              .Auditable(AuditEventType.SoftDelete, "LinkUser");
        /// </code>
        /// </example>
        public static RouteHandlerBuilder Auditable(this RouteHandlerBuilder builder, AuditEventType action,
            string resource, string? notes = null, string recordIdRouteKey = "id", string facilityIdRouteKey = "facilityId",
            bool suppressPropertyChanges = false)
        {
            ArgumentNullException.ThrowIfNull(builder);

            return builder.AddEndpointFilter(new AuditableEndpointFilter(action, resource, notes, recordIdRouteKey,
                facilityIdRouteKey, suppressPropertyChanges));
        }
    }
}
