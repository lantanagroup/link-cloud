using LantanaGroup.Link.Nhsn.App.Bff.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Errors;

public sealed class PatientListRetrievalFailedExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;

    public PatientListRetrievalFailedExceptionHandler(IProblemDetailsService problemDetailsService)
    {
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not PatientListRetrievalFailedException retrievalException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status424FailedDependency;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = retrievalException,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status424FailedDependency,
                Title = "A patient list could not be read from the EHR.",
                Detail = retrievalException.Message
            }
        });
    }
}
