using System.Net;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Terminology.Application.Interfaces;
using LantanaGroup.Link.Terminology.Application.Models;
using LantanaGroup.Link.Terminology.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace LantanaGroup.Link.Terminology.Controllers;

/// <summary>
/// Controller for managing configuration endpoints related to terminology.
/// Provides functionality to interact with and manage the terminology cache.
/// </summary>
[Route("api/terminology/config")]
[SwaggerTag("Configuration")]
public class ConfigController(ICodeGroupCacheService cacheService, ILogger<ConfigController> logger) : Controller
{
    /// <summary>
    /// Reloads the cache by clearing the existing data and repopulating it
    /// using the configured terminology path.
    /// </summary>
    /// <returns>An HTTP NoContent response indicating the operation was successful.</returns>
    [HttpPost("$reload-cache")]
    public async Task<ActionResult> ReloadCache()
    {
        logger.LogInformation("Reloading cache");
        cacheService.ClearCache();
        await cacheService.LoadCache();
        return NoContent();
    }

    /// <summary>
    /// Test/diagnostic endpoint that returns a single code from the cached CodeSystem
    /// identified by its resource <paramref name="id"/> (e.g. "v3-ActCode").
    /// </summary>
    /// <param name="id">The CodeSystem resource id.</param>
    /// <param name="code">The code value to look up within the CodeSystem.</param>
    /// <param name="version">Optional CodeSystem version; when omitted the latest cached version is used.</param>
    /// <returns>
    /// 200 with the matching code (value, display, status); 400 if <paramref name="id"/> or code is missing;
    /// 404 if the CodeSystem or code is not present in the cache.
    /// </returns>
    [HttpGet("code-systems/{id}/codes/{code}")]
    [SwaggerOperation(Summary = "Get a code from a cached CodeSystem by its resource id.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Code))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Code> GetCodeSystemCode(
        [FromRoute] string id,
        [FromRoute] string code,
        [FromQuery] string? version = null)
    {
        var cleanId = id?.Sanitize();
        var cleanCode = code?.Sanitize();

        // Treat a blank/whitespace-only ?version= the same as omitting it (null == latest version).
        var cleanVersion = string.IsNullOrWhiteSpace(version) ? null : version.Sanitize();

        if (string.IsNullOrWhiteSpace(cleanId))
        {
            ModelState.AddModelError("id", "A CodeSystem 'id' is required.");
        }

        if (string.IsNullOrWhiteSpace(cleanCode))
        {
            ModelState.AddModelError("code","A 'code' is required.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(
                title: "Bad Request",
                type: "https://datatracker.ietf.org/doc/html/rfc9457#section-3",
                detail: "One or more parameters were invalid.",
                statusCode: (int)HttpStatusCode.BadRequest,
                modelStateDictionary: ModelState);
        }

        var codeGroup = cacheService.GetCodeGroupById(CodeGroup.CodeGroupTypes.CodeSystem, cleanId, cleanVersion);
        if (codeGroup == null)
        {
            return Problem(
                detail: $"No CodeSystem found in the cache with id '{cleanId}' and version '{cleanVersion ?? "latest"}'.",
                statusCode: StatusCodes.Status404NotFound,
                title: "CodeSystem Not Found",
                type: "https://tools.ietf.org/html/rfc9110#section-15.5.5");
        }

        // A CSV may list the same code more than once with differing status; the last occurrence
        // wins (LEGLINK-599/814). LastOrDefault mirrors FhirService.BuildMatchResult's last-match
        // selection so this endpoint agrees with $validate-code.
        var match = codeGroup.Codes.Values
            .SelectMany(codes => codes)
            .LastOrDefault(c => string.Equals(c.Value, cleanCode, StringComparison.Ordinal));

        if (match == null)
        {
            return Problem(
                detail: $"Code '{cleanCode}' was not found in CodeSystem '{cleanId}'.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Code Not Found",
                type: "https://tools.ietf.org/html/rfc9110#section-15.5.5");
        }

        return Ok(match);
    }
}