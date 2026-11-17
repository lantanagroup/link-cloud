using System.Reflection;
using System.Security.Claims;
using LantanaGroup.Link.Shared.Application.Auditing;
using Link.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Claim = System.Security.Claims.Claim;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Tenant;

/// <summary>
/// Runs one of Tenant's controller actions the way MVC would with the shared audit filter attached,
/// so a test sees the event a real request publishes without standing up a web host.
/// </summary>
/// <remarks>
/// The attribute is read off the real controller method rather than restated here, which is what
/// makes these tests fail if an endpoint is ever marked differently from what the ADR's Tenant table
/// says.
/// </remarks>
internal sealed class AuditedEndpoint<TController> where TController : ControllerBase
{
    private static readonly IServiceProvider MvcServices = BuildMvcServices();

    private readonly AuditRecorder _recorder;

    public AuditedEndpoint(TController controller, IServiceProvider scopedServices, string callerEmail)
    {
        Controller = controller;
        _recorder = scopedServices.GetRequiredService<AuditRecorder>();

        var httpContext = new DefaultHttpContext
        {
            //the controllers reach IAuditContext through the request's services, so handing them this
            //scope is what lets a handler's RecordId reach the event the filter publishes.
            RequestServices = scopedServices,
            RequestAborted = CancellationToken.None,
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(LinkAuthorizationConstants.LinkSystemClaims.Email, callerEmail)],
                authenticationType: "TestBearer"))
        };

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.ProblemDetailsFactory = MvcServices.GetRequiredService<ProblemDetailsFactory>();
    }

    public TController Controller { get; }

    private static IServiceProvider BuildMvcServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// The attribute the running service would use for this action.
    /// </summary>
    public static AuditableAttribute AttributeOn(string actionName) =>
        typeof(TController).GetMethod(actionName)!.GetCustomAttribute<AuditableAttribute>()
        ?? throw new InvalidOperationException($"{typeof(TController).Name}.{actionName} is not marked [Auditable].");

    /// <summary>
    /// What a controller action returning <see cref="ActionResult{TValue}"/> actually produced.
    /// </summary>
    public static IActionResult Unwrap<TValue>(ActionResult<TValue> result) =>
        result.Result ?? new ObjectResult(result.Value);

    /// <summary>
    /// The action, then the execution of the result it returned, which is the stage that publishes.
    /// </summary>
    public async Task RunAsync(string actionName, RouteValueDictionary routeValues,
        Func<Task<IActionResult>> action)
    {
        var filter = new AuditableFilter(AttributeOn(actionName), _recorder);
        var actionContext = new ActionContext(Controller.HttpContext, new RouteData(routeValues),
            new ActionDescriptor());
        var executing = new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), Controller);

        IActionResult? result = null;
        await filter.OnActionExecutionAsync(executing, async () =>
            new ActionExecutedContext(actionContext, [], Controller) { Result = result = await action() });

        var executed = result ?? new EmptyResult();
        var resultExecuting = new ResultExecutingContext(actionContext, [], executed, Controller);

        await filter.OnResultExecutionAsync(resultExecuting, () =>
        {
            actionContext.HttpContext.Response.StatusCode =
                executed is IStatusCodeActionResult { StatusCode: { } code } ? code : StatusCodes.Status200OK;

            return Task.FromResult(new ResultExecutedContext(actionContext, [], executed, Controller));
        });
    }

    /// <summary>
    /// For an action declared as <see cref="ActionResult{TValue}"/>.
    /// </summary>
    public Task RunAsync<TValue>(string actionName, RouteValueDictionary routeValues,
        Func<Task<ActionResult<TValue>>> action) =>
        RunAsync(actionName, routeValues, async () => Unwrap(await action()));

    /// <summary>
    /// For an action on a route whose only key is the record's own id.
    /// </summary>
    public Task RunAsync(string actionName, string routeKey, string routeValue,
        Func<Task<IActionResult>> action) =>
        RunAsync(actionName, new RouteValueDictionary { [routeKey] = routeValue }, action);

    public Task RunAsync<TValue>(string actionName, string routeKey, string routeValue,
        Func<Task<ActionResult<TValue>>> action) =>
        RunAsync(actionName, new RouteValueDictionary { [routeKey] = routeValue },
            async () => Unwrap(await action()));
}
