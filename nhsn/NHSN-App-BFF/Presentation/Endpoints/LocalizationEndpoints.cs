using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

namespace LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;

public sealed class LocalizationEndpoints : IApi
{
    public void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/localization")
            .WithTags("Localization")
            .WithOpenApi();

        group.MapGet("/{locale}/{namespaceName}", async (string locale, string namespaceName, HttpContext context, ILocalizationResourceService localizationResourceService, CancellationToken cancellationToken) =>
            {
                var result = await localizationResourceService.GetNamespaceAsync(locale, namespaceName, cancellationToken);
                return result.Status switch
                {
                    LocalizationResourceStatus.Ok => HandleOkResult(context, result),
                    LocalizationResourceStatus.InvalidLocale or LocalizationResourceStatus.InvalidNamespace => Results.BadRequest(new { message = result.Message }),
                    LocalizationResourceStatus.NotFound => Results.NotFound(new { message = result.Message }),
                    LocalizationResourceStatus.MalformedJson => Results.Problem(title: "Invalid localization data", detail: result.Message, statusCode: StatusCodes.Status500InternalServerError),
                    LocalizationResourceStatus.DirectoryUnavailable => Results.Problem(title: "Localization resources unavailable", detail: result.Message, statusCode: StatusCodes.Status503ServiceUnavailable),
                    _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
                };
            })
            .WithName("GetLocalizationNamespace")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Get localization namespace resources.";
                operation.Description = "Returns localized JSON payload for the requested locale and namespace with locale fallback to en-US.";
                return operation;
            });
    }

    private static IResult HandleOkResult(HttpContext context, LocalizationResourceResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.ETag))
        {
            var expectedETag = $"\"{result.ETag}\"";
            context.Response.Headers.ETag = expectedETag;

            var ifNoneMatch = context.Request.Headers.IfNoneMatch.ToString();
            if (string.Equals(ifNoneMatch, expectedETag, StringComparison.Ordinal))
            {
                return Results.StatusCode(StatusCodes.Status304NotModified);
            }
        }

        if (result.LastModified.HasValue)
        {
            context.Response.Headers.LastModified = result.LastModified.Value.ToString("R");
        }

        return Results.Content(result.JsonPayload ?? "{}", "application/json");
    }
}