using Link.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Link.UI.Controllers;

/// <summary>
/// Automation template pages. The stores exist only when the in-process engine is ready.
/// </summary>
public abstract class AutomationTemplateController : Controller
{
    private readonly LinkAutomationEngineStatus _engine;

    protected AutomationTemplateController(LinkAutomationEngineStatus engine, IServiceProvider services)
    {
        _engine = engine;
        Services = services;
    }

    protected IServiceProvider Services { get; }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!_engine.Ready || !AttachStores())
        {
            context.Result = new ContentResult
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable,
                Content = _engine.Message ?? AutomationRunReader.NotConfiguredMessage,
                ContentType = "text/plain; charset=utf-8"
            };
            return;
        }

        ViewData["AutomationSection"] = "configurations";
        base.OnActionExecuting(context);
    }

    protected abstract bool AttachStores();

    protected bool Resolve<T>(out T service) where T : class
    {
        service = Services.GetService<T>()!;
        return service is not null;
    }
}
