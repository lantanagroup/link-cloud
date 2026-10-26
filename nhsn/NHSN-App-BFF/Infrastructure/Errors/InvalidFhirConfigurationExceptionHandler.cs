using LantanaGroup.Link.Nhsn.App.Bff.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Errors;

public sealed class InvalidFhirConfigurationExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;

    public InvalidFhirConfigurationExceptionHandler(IProblemDetailsService problemDetailsService)
    {
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not InvalidFhirConfigurationException invalidException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = invalidException,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The FHIR configuration could not be saved.",
                Detail = invalidException.Message,
                Extensions = { ["errorCode"] = invalidException.ErrorCode }
            }
        });
    }
}
