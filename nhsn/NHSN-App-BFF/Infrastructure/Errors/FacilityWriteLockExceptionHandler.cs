using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Errors;

// Turns write-lock contention into 409 Conflict rather than a 500. A 500 would read as our bug —
// the honest meaning is "another save for this facility is in flight, try again", an expected
// outcome of two tabs rather than a fault.
public sealed class FacilityWriteLockExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;

    public FacilityWriteLockExceptionHandler(IProblemDetailsService problemDetailsService)
    {
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not FacilityWriteLockTimeoutException lockException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = lockException,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Another save for this facility is in progress.",
                Detail = $"The write could not start within {lockException.TimeoutMs}ms. Retry the save."
            }
        });
    }
}
