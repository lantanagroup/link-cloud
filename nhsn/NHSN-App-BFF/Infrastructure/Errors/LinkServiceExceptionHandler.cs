using LantanaGroup.Link.Nhsn.App.Bff.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Errors;

// Turns a LinkServiceException into RFC 7807 ProblemDetails with status 502.
//
// A downstream failure is a bad gateway, not a server error — surfacing it as 500 would make every
// Link outage look like our bug. The downstream TraceId goes into extensions so a facility's
// report can be correlated to the Link-side log in Loki. The downstream body is deliberately not
// forwarded — its shape varies by service and it can carry configuration detail that shouldn't
// reach a browser — so it's logged instead.
public sealed class LinkServiceExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<LinkServiceExceptionHandler> _logger;

    public LinkServiceExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<LinkServiceExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not LinkServiceException linkException)
        {
            return false;
        }

        _logger.LogError(
            linkException,
            "Link call failed. Service={Service}; Operation={Operation}; Status={StatusCode}; DownstreamTraceId={DownstreamTraceId}; RequestUrl={RequestUrl}; Body={RawBody}",
            linkException.Service,
            linkException.Operation,
            linkException.StatusCode,
            linkException.TraceId ?? "<none>",
            linkException.RequestUrl ?? "<none>",
            linkException.RawBody ?? "<none>");

        httpContext.Response.StatusCode = StatusCodes.Status502BadGateway;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = linkException,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status502BadGateway,
                Title = "A downstream Link service could not complete the request.",
                Detail = $"{linkException.Service} returned {linkException.StatusCode}.",
                Extensions =
                {
                    ["downstreamService"] = linkException.Service,
                    ["downstreamStatusCode"] = linkException.StatusCode,
                    ["downstreamTraceId"] = linkException.TraceId
                }
            }
        });
    }
}
