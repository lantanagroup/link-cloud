using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

namespace LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;

// Serves the vendor instruction documents named by VendorProfile.DocumentKeys (census
// instructions, JWKS instructions, org-resolution guidance). One route, keyed rather than
// path-based, so the client never names a file directly.
public sealed class DocumentsEndpoints : IApi
{
    public void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/nhsn-app-bff/documents")
            .WithTags("Documents")
            .RequireAuthorization("AuthenticatedUser");

        group.MapGet("/{documentKey}", async (string documentKey, IDocumentProvider documentProvider, CancellationToken cancellationToken) =>
            {
                var result = await documentProvider.GetAsync(documentKey, cancellationToken);
                return result.Status switch
                {
                    DocumentStatus.Ok => Results.File(result.Content!, result.ContentType!, result.FileName),
                    DocumentStatus.NotFound => Results.NotFound(new {message = "The requested document was not found."}),
                    DocumentStatus.DirectoryUnavailable => Results.Problem(
                        title: "Document resources unavailable",
                        statusCode: StatusCodes.Status503ServiceUnavailable),
                    _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
                };
            })
            .WithName("GetDocument")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Get a vendor instruction document by key.";
                operation.Description =
                    "documentKey comes from the vendor profile (VendorProfile.documentKeys) and is " +
                    "resolved against a fixed allow-list — it is never used to build a file path " +
                    "directly.";
                return operation;
            });
    }
}
