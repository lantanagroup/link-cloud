using LantanaGroup.Link.MockDmrpApi.Application.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;

namespace LantanaGroup.Link.MockDmrpApi.Application.Middleware;

/// <summary>
/// Short-circuits every request with 503 when the stand-in is disabled.
/// </summary>
/// <remarks>
/// Registered before routing, so a disabled deployment cannot reach a controller no matter
/// what is added later. The alternatives were worse: refusing to start crash-loops the pod
/// and pages someone, and skipping route registration produces a 404 that is
/// indistinguishable from a typo'd path or a misconfigured ingress.
/// <para>
/// 503 is also the honest answer -- the service exists and is reachable, it just will not
/// serve this environment.
/// </para>
/// </remarks>
public class DmrpDisabledMiddleware
{
    private readonly RequestDelegate _next;
    private readonly bool _enabled;

    public DmrpDisabledMiddleware(
        RequestDelegate next, IHostEnvironment environment, IConfiguration configuration)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _enabled = DmrpAvailability.IsEnabled(environment, configuration);
    }

    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problemDetails)
    {
        if (_enabled || DmrpAvailability.IsAlwaysAvailable(context.Request.Path))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        var problem = new ProblemDetails
        {
            Title = "Mock DMRP API is disabled",
            Status = StatusCodes.Status503ServiceUnavailable,
            Type = DmrpProblemTypes.ServiceUnavailable,
            Detail = "This deployment does not serve the mock DMRP surface. It is disabled by "
                     + "configuration, or running in an environment where it is never enabled.",
            Instance = context.Request.Path
        };

        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;

        var written = await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem
        });

        if (!written)
        {
            // Nothing is registered to render problem details. Still refuse the request --
            // the status code is the part that matters.
            await context.Response.WriteAsync(problem.Title!, context.RequestAborted);
        }
    }
}

public static class DmrpDisabledMiddlewareExtensions
{
    public static IApplicationBuilder UseDmrpAvailabilityGate(this IApplicationBuilder app) =>
        app.UseMiddleware<DmrpDisabledMiddleware>();
}
