using System.Net;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Terminology;
using LantanaGroup.Link.Terminology.Application.Interfaces;
using LantanaGroup.Link.Terminology.Application.Models;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace LantanaGroup.Link.Terminology.Controllers;

/// <summary>
/// Searches the loaded terminology content for codes, for callers that need a concise result set rather
/// than a FHIR resource.
/// </summary>
/// <param name="codeSearchService">Runs the search against the cached code groups.</param>
/// <param name="logger">Records failures; the search path itself is not logged, as the UI calls it per keystroke.</param>
[Route("api/terminology/codes")]
[SwaggerTag("Code Search Operations")]
[ApiController]
public class CodesController(ICodeSearchService codeSearchService, ILogger<CodesController> logger) : Controller
{
    /// <summary>
    /// Searches the loaded terminology content for codes matching a caller's query.
    /// </summary>
    /// <param name="query">The search parameters, bound from the query string.</param>
    /// <param name="cancellationToken">Token to indicate if the operation should be cancelled.</param>
    /// <returns>
    /// 200 with the matching page, whose <c>metadata.totalCount</c> is the full match count before paging;
    /// 400 with Problem Details naming the offending parameter when the query is unusable or names a code
    /// system, value set or version that is not loaded. A search matching nothing is a successful search:
    /// it returns 200 with an empty <c>records</c> array.
    /// </returns>
    [HttpGet]
    [SwaggerOperation(
        Summary = "Search cached CodeSystems and ValueSets for codes.",
        Description = "Supply at least one of search, codeSystem or valueSet. search is a case-insensitive "
                      + "contains match against both the code and its display, and must be at least three "
                      + "characters. codeSystem and valueSet are mutually exclusive canonical URIs.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedConfigModel<TerminologyCodeModel>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] CodeSearchQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        query ??= new CodeSearchQuery();

        ModelState.Merge(query.Validate());

        if (!ModelState.IsValid)
        {
            return ValidationProblem(
                title: "Bad Request",
                type: "https://datatracker.ietf.org/doc/html/rfc9457#section-3",
                detail: "One or more parameters were invalid.",
                statusCode: (int)HttpStatusCode.BadRequest,
                modelStateDictionary: ModelState);
        }

        try
        {
            var results = await codeSearchService.Search(query, cancellationToken);
            return Ok(results);
        }
        catch (ArgumentException ex)
        {
            // The service rejects a code system, value set or version that is not loaded, naming the
            // parameter in ParamName. That is client input that failed validation, so it is a 400: 404 is
            // reserved for a single resource fetched by id, and these are filters on a search.
            ModelState.AddModelError(ex.ParamName ?? CodeSearchParameters.Search, ex.Message);

            return ValidationProblem(
                title: "Bad Request",
                type: "https://datatracker.ietf.org/doc/html/rfc9457#section-3",
                detail: "One or more parameters were invalid.",
                statusCode: (int)HttpStatusCode.BadRequest,
                modelStateDictionary: ModelState);
        }
        catch (OperationCanceledException)
        {
            // The caller gave up mid-scan. Let it propagate rather than reporting a server fault for a
            // request nobody is waiting on any more.
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Code search failed");
            return InternalServerErrorProblem(ex.Message);
        }
    }

    /// <summary>
    /// Builds an RFC 9457 Problem Details result, matching the shape <see cref="FhirController"/> and
    /// <see cref="ConfigController"/> already return. The <c>traceId</c> extension is added by the
    /// service-wide customization in <c>TerminologyProblemDetailsExtensions</c>.
    /// </summary>
    private ObjectResult TerminologyProblem(HttpStatusCode statusCode, string title, string type, string detail)
    {
        var sentence = detail.EndsWith('.') || detail.EndsWith('?') || detail.EndsWith('!')
            ? detail
            : detail + ".";

        return Problem(detail: sentence, statusCode: (int)statusCode, title: title, type: type);
    }

    /// <summary>
    /// The search could not be completed. RFC 9110 section 15.6.1. The customization replaces
    /// <c>detail</c> with a generic message so internal state is not exposed to the caller.
    /// </summary>
    private ObjectResult InternalServerErrorProblem(string detail) => TerminologyProblem(
        HttpStatusCode.InternalServerError, "Internal Server Error",
        "https://tools.ietf.org/html/rfc9110#section-15.6.1", detail);
}
