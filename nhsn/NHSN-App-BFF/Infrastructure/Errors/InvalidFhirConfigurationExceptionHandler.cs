using LantanaGroup.Link.Nhsn.App.Bff.Domain.Exceptions;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Errors;

public sealed class InvalidFhirConfigurationExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<InvalidFhirConfigurationExceptionHandler> _logger;

    public InvalidFhirConfigurationExceptionHandler(IProblemDetailsService problemDetailsService,
                                                    ILogger<InvalidFhirConfigurationExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not InvalidFhirConfigurationException invalidException)
        {
            return false;
        }

        // .NET 10 no longer logs an exception a handler takes care of, so log it here.
        _logger.LogWarning(invalidException,
                           "FHIR configuration for {FacilityId} rejected with {ErrorCode}; returned 400.",
                           invalidException.FacilityId.SanitizeForLog(),
                           invalidException.ErrorCode.SanitizeForLog());

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
