using LantanaGroup.Link.Nhsn.App.Bff.Domain.Exceptions;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Errors;

public sealed class PatientListRetrievalFailedExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<PatientListRetrievalFailedExceptionHandler> _logger;

    public PatientListRetrievalFailedExceptionHandler(IProblemDetailsService problemDetailsService,
                                                      ILogger<PatientListRetrievalFailedExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not PatientListRetrievalFailedException retrievalException)
        {
            return false;
        }

        // .NET 10 no longer logs an exception a handler takes care of, so log it here.
        _logger.LogWarning(retrievalException,
                           "Patient list {ListKey} for {FacilityId} could not be read from the EHR; returned 424.",
                           (retrievalException.ListKey ?? "<none>").SanitizeForLog(),
                           retrievalException.FacilityId.SanitizeForLog());

        httpContext.Response.StatusCode = StatusCodes.Status424FailedDependency;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = retrievalException,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status424FailedDependency,
                Title = "A patient list could not be read from the EHR.",
                Detail = retrievalException.Message,
                Extensions = { ["listKey"] = retrievalException.ListKey }
            }
        });
    }
}
