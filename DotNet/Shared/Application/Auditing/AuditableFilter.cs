using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace LantanaGroup.Link.Shared.Application.Auditing
{
    /// <summary>
    /// Wraps a controller action marked <see cref="AuditableAttribute"/>: one audit event once the
    /// action's result has executed, and only if executing it succeeded.
    /// </summary>
    /// <remarks>
    /// MVC runs the action and its result in two separate stages, so this filter hooks both. Around
    /// the action it opens the recording scope and judges what the action returned; around the result
    /// it sees the status that execution set and whether execution failed, and publishes there.
    /// Publishing from the action stage alone would describe a request that still had result
    /// execution and serialisation ahead of it, so a result that then failed to execute left a
    /// success event behind a failed response.
    /// <para>
    /// What the action did to the database is the interceptor's half; all this decides is whether the
    /// request succeeded.
    /// </para>
    /// <para>
    /// The result stage is <see cref="IAsyncAlwaysRunResultFilter"/> so that it still runs for a
    /// result an exception filter produced, where the ordinary result pipeline is skipped and an
    /// event must not be published. Implementing it also implements
    /// <see cref="IAsyncResultFilter"/>, which is the normal path, and MVC invokes one or the other -
    /// never both - so there is no second publish.
    /// </para>
    /// </remarks>
    public sealed class AuditableFilter : IAsyncActionFilter, IAsyncAlwaysRunResultFilter
    {
        private readonly AuditableAttribute _auditable;
        private readonly AuditRecorder _recorder;

        /// <summary>
        /// Whether the action stage judged the action a success, carried to the result stage.
        /// </summary>
        /// <remarks>
        /// Per-request state on the filter instance, which <see cref="AuditableAttribute.IsReusable"/>
        /// being false guarantees: MVC builds the filter once per request and visits that one instance
        /// in both pipelines.
        /// <para>
        /// It starts false, which is what stops a request short-circuited before the action from
        /// publishing anything - an authorization filter, or <c>[ApiController]</c>'s model-state
        /// filter, means this action stage never runs while the always-run result stage still does.
        /// </para>
        /// </remarks>
        private bool _actionSucceeded;

        public AuditableFilter(AuditableAttribute auditable, AuditRecorder recorder)
        {
            _auditable = auditable ?? throw new ArgumentNullException(nameof(auditable));
            _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(next);

            _recorder.Begin();

            //an action that throws reaches no result stage at all: no event describes an action that
            //did not succeed.
            var executed = await next();

            //an exception nobody handled is still on its way up, so the action did not succeed.
            var threw = executed.Exception is not null && !executed.ExceptionHandled;
            _actionSucceeded = !threw && IsSuccess(executed.Result);
        }

        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(next);

            var executed = await next();

            //nothing was audited on the way in, or another filter stopped the result from running.
            if (!_actionSucceeded || executed.Canceled)
            {
                return;
            }

            //this is tested before the status code, not after: a serialiser that fails part-way
            //through the body leaves the status at the 200 already set, so the exception is the only
            //evidence that the caller did not get what the event would claim.
            //
            //ExceptionHandled is deliberately not consulted here, unlike in the action stage. There a
            //filter handling the exception means it substituted a result that was then executed, so
            //the caller did get a response; here there is no result left to substitute and nothing
            //re-executes, so handling the exception only swallows it - the caller is still left with
            //a truncated body or an empty success.
            if (executed.Exception is not null)
            {
                return;
            }

            if (!IsSuccess(context.HttpContext.Response.StatusCode))
            {
                return;
            }

            await _recorder.RecordAsync(_auditable, context.HttpContext.User,
                context.RouteData.Values, context.HttpContext.Request.Query);
        }

        /// <summary>
        /// Whether the result the action produced is a success, as far as the action stage can tell.
        /// </summary>
        /// <remarks>
        /// The response is not written yet when the action stage runs, so the result object is all
        /// there is to go on, and the status it really wrote is checked again in the result stage.
        /// Anything carrying a status code is judged by it; anything that carries none, such as
        /// EmptyResult, becomes a 200 and counts as success here - the result stage sees what it
        /// actually wrote.
        /// </remarks>
        private static bool IsSuccess(IActionResult? result) => result switch
        {
            null => false,
            IStatusCodeActionResult { StatusCode: { } code } => IsSuccess(code),
            _ => true
        };

        private static bool IsSuccess(int statusCode) => statusCode is >= 200 and < 300;
    }
}
