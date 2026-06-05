using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace LantanaGroup.Link.Shared.Application.Extensions;

public static class ModelStateProblemExtensions
{
    private const string Rfc7807Type = "https://datatracker.ietf.org/doc/html/rfc7807#section-3";

    /// <summary>
    /// Builds an RFC 7807 (application/problem+json) 400 response from the
    /// controller's current ModelState, exposing each error under "invalid-params".
    /// When an HttpContext is available the ProblemDetails is created via
    /// <see cref="ProblemDetailsFactory"/>, so it picks up the same enrichment
    /// (e.g. traceId) as <c>ControllerBase.Problem(...)</c>.
    /// </summary>
    public static BadRequestObjectResult InvalidParametersProblem(
        this ControllerBase controller,
        string title = "Your request parameters didn't validate.")
    {
        var validationErrors = controller.ModelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .Select(kvp => (
                name: kvp.Key,
                reason: string.Join("; ", kvp.Value!.Errors.Select(e => e.ErrorMessage))))
            .ToList();

        var factory = controller.HttpContext?.RequestServices?.GetService<ProblemDetailsFactory>();

        var problems = factory is not null
            ? factory.CreateProblemDetails(
                controller.HttpContext!,
                statusCode: (int)HttpStatusCode.BadRequest,
                title: title,
                type: Rfc7807Type)
            : new ProblemDetails
            {
                Type = Rfc7807Type,
                Title = title,
                Status = (int)HttpStatusCode.BadRequest
            };

        problems.Extensions["invalid-params"] = validationErrors;

        return controller.BadRequest(problems);
    }
}
