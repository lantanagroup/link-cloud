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
public class ConfigController(
    ICodeGroupCacheService cacheService,
    FhirService fhirService,
    ILogger<ConfigController> logger) : Controller
{
    /// <summary>
    /// Sanitizes a route or query value that is matched against loaded terminology content.
    /// </summary>
    /// <remarks>
    /// <see cref="HtmlInputSanitizer.Sanitize"/> strips markup but also HTML-encodes what survives, so a
    /// code or system URI containing a reserved character stops matching the cache — an "&amp;" arrives as
    /// "&amp;amp;" and the lookup 404s on content that is present. Decoding afterwards restores the plain
    /// text while still dropping any markup. <c>FhirController.SanitizeTerminologyValue</c> does the same for
    /// the same reason; the two must agree or a code found by <c>$validate-code</c> is missing here.
    /// </remarks>
    private static string? SanitizeLookupValue(string? value) =>
        value is null ? null : WebUtility.HtmlDecode(value.Sanitize());

    /// <summary>
    /// Builds the 404 Problem Details shape shared by the cached-code lookups.
    /// </summary>
    private ObjectResult NotFoundProblem(string title, string detail) => Problem(
        detail: detail,
        statusCode: StatusCodes.Status404NotFound,
        title: title,
        type: "https://tools.ietf.org/html/rfc9110#section-15.5.5");

    /// <summary>
    /// Accumulates the "required" checks the cached-code lookups share, returning a 400 Problem Details
    /// result when either identifier is missing and <c>null</c> when the request is usable.
    /// </summary>
    /// <remarks>
    /// Both errors are collected before returning rather than short-circuiting on the first, so a caller
    /// that omitted both is told about both.
    /// </remarks>
    private ActionResult? ValidateLookupParameters(string? id, string? code, string idParameterName)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            ModelState.AddModelError("id", $"A {idParameterName} 'id' is required.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            ModelState.AddModelError("code", "A 'code' is required.");
        }

        if (ModelState.IsValid)
        {
            return null;
        }

        return ValidationProblem(
            title: "Bad Request",
            type: "https://datatracker.ietf.org/doc/html/rfc9457#section-3",
            detail: "One or more parameters were invalid.",
            statusCode: (int)HttpStatusCode.BadRequest,
            modelStateDictionary: ModelState);
    }

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
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CodeSystemCode))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<CodeSystemCode> GetCodeSystemCode(
        [FromRoute] string id,
        [FromRoute] string code,
        [FromQuery] string? version = null)
    {
        var cleanId = SanitizeLookupValue(id);
        var cleanCode = SanitizeLookupValue(code);

        // Treat a blank/whitespace-only ?version= the same as omitting it (null == latest version).
        var cleanVersion = string.IsNullOrWhiteSpace(version) ? null : version.Sanitize();

        var invalid = ValidateLookupParameters(cleanId, cleanCode, "CodeSystem");
        if (invalid != null)
        {
            return invalid;
        }

        var codeGroup = cacheService.GetCodeGroupById(CodeGroup.CodeGroupTypes.CodeSystem, cleanId!, cleanVersion);
        if (codeGroup == null)
        {
            return NotFoundProblem(
                "CodeSystem Not Found",
                $"No CodeSystem found in the cache with id '{cleanId}' and version '{cleanVersion ?? "latest"}'.");
        }

        // A CSV may list the same code more than once with differing status; the last occurrence
        // wins (LEGLINK-599/814). LastOrDefault mirrors FhirService.BuildMatchResult's last-match
        // selection so this endpoint agrees with $validate-code.
        //
        // OfType matches FhirService.ResolveCodeStatus: ProcessCodeSystemCsv only ever adds
        // CodeSystemCode to a CodeSystem group, so this narrows the declared response type to the one
        // that actually carries a status rather than filtering anything real out.
        var match = codeGroup.Codes.Values
            .SelectMany(codes => codes)
            .OfType<CodeSystemCode>()
            .LastOrDefault(c => string.Equals(c.Value, cleanCode, StringComparison.Ordinal));

        if (match == null)
        {
            return NotFoundProblem(
                "Code Not Found",
                $"Code '{cleanCode}' was not found in CodeSystem '{cleanId}'.");
        }

        return Ok(match);
    }

    /// <summary>
    /// Test/diagnostic endpoint that returns a single member of the cached ValueSet identified by its
    /// resource <paramref name="id"/> (e.g. "address-type"), reporting both the status the value set
    /// declares for it and the status that will actually be applied.
    /// </summary>
    /// <remarks>
    /// The counterpart to <see cref="GetCodeSystemCode"/>, and the only way to read a value set's own
    /// membership status back. Without it the two were easy to confuse: marking a code inactive in a value
    /// set CSV and then reading it back through the CodeSystem lookup returns the code system's untouched
    /// status, which looks like the edit was ignored (LEGLINK-889).
    /// </remarks>
    /// <param name="id">The ValueSet resource id.</param>
    /// <param name="code">The code value to look up within the ValueSet.</param>
    /// <param name="system">
    /// Optional code system URI. A value set groups its members by system and may list the same code under
    /// more than one; supplying this restricts the search to that system, and omitting it takes the first
    /// system that lists the code — the same selection <c>$validate-code</c> makes.
    /// </param>
    /// <param name="version">Optional ValueSet version; when omitted the latest cached version is used.</param>
    /// <returns>
    /// 200 with the matching member; 400 if <paramref name="id"/> or <paramref name="code"/> is missing;
    /// 404 if the ValueSet or the code is not present in the cache.
    /// </returns>
    [HttpGet("value-sets/{id}/codes/{code}")]
    [SwaggerOperation(Summary = "Get a member of a cached ValueSet by its resource id.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ValueSetCodeLookupResult))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ValueSetCodeLookupResult> GetValueSetCode(
        [FromRoute] string id,
        [FromRoute] string code,
        [FromQuery] string? system = null,
        [FromQuery] string? version = null)
    {
        var cleanId = SanitizeLookupValue(id);
        var cleanCode = SanitizeLookupValue(code);

        // Blank/whitespace-only values are treated the same as omitting the parameter: null version means
        // the latest cached version, null system means "search every system in the value set".
        var cleanVersion = string.IsNullOrWhiteSpace(version) ? null : version.Sanitize();
        var cleanSystem = string.IsNullOrWhiteSpace(system) ? null : SanitizeLookupValue(system);

        var invalid = ValidateLookupParameters(cleanId, cleanCode, "ValueSet");
        if (invalid != null)
        {
            return invalid;
        }

        var codeGroup = cacheService.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, cleanId!, cleanVersion);
        if (codeGroup == null)
        {
            return NotFoundProblem(
                "ValueSet Not Found",
                $"No ValueSet found in the cache with id '{cleanId}' and version '{cleanVersion ?? "latest"}'.");
        }

        var (matchedSystem, match) = FindValueSetMember(codeGroup, cleanCode!, cleanSystem);

        if (match == null)
        {
            var scope = cleanSystem == null ? string.Empty : $" under system '{cleanSystem}'";
            return NotFoundProblem(
                "Code Not Found",
                $"Code '{cleanCode}' was not found in ValueSet '{cleanId}'{scope}.");
        }

        return Ok(new ValueSetCodeLookupResult
        {
            System = matchedSystem!,
            Value = match.Value,
            Display = match.Display,

            // A member loaded as a plain Code came from a value set with no status column, so the value
            // set declares nothing about it. That is reported as null rather than Active, which would
            // claim the value set had made a statement it never made.
            MembershipStatus = match is ValueSetCode valueSetCode ? valueSetCode.Status : null,
            EffectiveStatus = fhirService.ResolveCodeStatus(match, matchedSystem)
        });
    }

    /// <summary>
    /// Locates a value set member, mirroring how <c>ValueSet/$validate-code</c> selects one so that the two
    /// never disagree about which occurrence of a code they are talking about.
    /// </summary>
    /// <remarks>
    /// A caller-supplied system narrows the search to that system's members. Otherwise the systems are
    /// walked in load order and the first one listing the code wins, matching
    /// <c>FhirService.ValidateCodeAcrossSystems</c>. Within a system the last occurrence wins, matching
    /// <c>FhirService.BuildMatchResult</c> — a CSV may list the same code twice with differing status and
    /// the later row is the effective one (LEGLINK-599/814).
    /// </remarks>
    private static (string? System, Code? Match) FindValueSetMember(CodeGroup codeGroup, string code, string? system)
    {
        if (system != null)
        {
            if (!codeGroup.Codes.TryGetValue(system, out var systemCodes))
            {
                return (system, null);
            }

            return (system, systemCodes.LastOrDefault(c => string.Equals(c.Value, code, StringComparison.Ordinal)));
        }

        foreach (var systemKey in codeGroup.Codes.Keys)
        {
            var candidate = codeGroup.Codes[systemKey]
                .LastOrDefault(c => string.Equals(c.Value, code, StringComparison.Ordinal));

            if (candidate != null)
            {
                return (systemKey, candidate);
            }
        }

        return (null, null);
    }
}