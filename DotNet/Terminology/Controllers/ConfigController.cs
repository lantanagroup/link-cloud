using System.Net;
using CsvHelper;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Terminology.Application.Exceptions;
using LantanaGroup.Link.Terminology.Application.Interfaces;
using LantanaGroup.Link.Terminology.Application.Models;
using LantanaGroup.Link.Terminology.Application.Settings;
using LantanaGroup.Link.Terminology.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
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
    IOptions<TerminologyConfig> terminologyConfig,
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
    public async Task<ActionResult> ReloadCache(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Reloading cache");
        cacheService.ClearCache();
        await cacheService.LoadCache(cancellationToken);
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
    /// system that lists the code — the same selection <c>$validate-code</c> makes. Supplying a value that
    /// sanitizes away to nothing is rejected rather than treated as omitted, since widening the search is
    /// not what the caller asked for.
    /// </param>
    /// <param name="version">Optional ValueSet version; when omitted the latest cached version is used.</param>
    /// <returns>
    /// 200 with the matching member; 400 if <paramref name="id"/> or <paramref name="code"/> is missing or
    /// <paramref name="system"/> is unusable; 404 if the ValueSet or the code is not present in the cache.
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

        // A supplied system that sanitizes away to nothing is rejected rather than quietly treated as
        // omitted. Omitting the system widens the search to every system in the value set, so silently
        // dropping an unusable one could answer with a match under a system the caller never asked about —
        // a wrong answer dressed as a right one. FhirController.SanitizeTerminologyValue rejects an emptied
        // value for the same reason. Note the blank check is on the SANITIZED value, not the raw one:
        // markup-only input like "<b></b>" is not whitespace but sanitizes to nothing.
        string? cleanSystem = null;
        if (!string.IsNullOrWhiteSpace(system))
        {
            cleanSystem = SanitizeLookupValue(system);

            if (string.IsNullOrWhiteSpace(cleanSystem))
            {
                ModelState.AddModelError("system", "Invalid value supplied for 'system'.");
            }
        }

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

    #region Code upload (non-production)

    /// <summary>
    /// The authorization policy the upload endpoints require. These replace the codes every downstream
    /// validation is judged against, so they are the one part of this service that is not left open.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This raises the bar wherever authentication is actually configured, which is every deployed
    /// environment: <c>Authentication:EnableAnonymousAccess</c> is false for Terminology in dev, qa, qa2 and
    /// test, so the real <c>IsLinkAdmin</c> policy applies and an unauthenticated or non-admin caller is
    /// refused.
    /// </para>
    /// <para>
    /// It deliberately does <b>not</b> claim to hold when anonymous access is on.
    /// <c>AddLinkBearerServiceAuthentication</c> returns before registering any authentication scheme in that
    /// mode and defines every named policy as <c>RequireAssertion(context =&gt; true)</c>, so this passes for
    /// anyone — as does every other endpoint in the service, which declares no policy at all. A bare
    /// <c>[Authorize]</c> would not close that gap either: with no default challenge scheme registered it
    /// throws rather than returning 401. Closing it properly means an explicit authenticated-user check,
    /// which would make these endpoints permanently unusable under docker-compose, the environment they
    /// exist to serve. The remaining protection there is <c>Terminology:EnableCodeUploadEndpoint</c>, which
    /// ships false.
    /// </para>
    /// </remarks>
    private const string CodeUploadPolicy = "IsLinkAdmin";

    /// <summary>
    /// The largest CSV this endpoint will accept. Sized to comfortably exceed the terminology artifacts
    /// shipped in the data volume; it exists to bound the in-memory read, not to express a policy about
    /// terminology size. Raise it if a legitimate artifact ever exceeds it.
    /// </summary>
    private const long MaxCsvUploadBytes = 32L * 1024 * 1024;

    /// <summary>Extra room for the multipart framing that wraps the file itself.</summary>
    private const long MaxRequestBytes = MaxCsvUploadBytes + 8 * 1024;

    /// <summary>
    /// Replaces the codes of a cached ValueSet with the contents of an uploaded CSV.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Testing aid, disabled by default.</b> Enabled by <c>Terminology:EnableCodeUploadEndpoint</c>;
    /// while it is false this route reports 404. The replacement lives in the memory of the single
    /// instance that served the request — nothing is written to the terminology data volume, and the
    /// FHIR resource metadata (url, version, id, name, identifiers) is preserved exactly. Revert with
    /// <c>POST /api/terminology/config/$reload-cache</c>, or by restarting the service.
    /// </para>
    /// <para>
    /// <b>Do not enable where more than one instance is running.</b> The upload reaches one instance
    /// only, so subsequent validation calls would be answered inconsistently depending on which
    /// instance handled them, with nothing in the response to indicate it.
    /// </para>
    /// <para>
    /// The CSV <b>must start with a header row</b>, which is skipped without being inspected — columns
    /// are read by position, so a file without one loses its first code. Columns are
    /// <c>system, code, display</c> plus an optional <c>status</c> of Active or Inactive. Supplying the
    /// status column is what gives members their own value-set membership status; omit it and each
    /// code's status is resolved from its code system instead.
    /// </para>
    /// </remarks>
    /// <param name="id">The ValueSet resource id, e.g. "v3-ActEncounterCode".</param>
    /// <param name="file">The CSV file, sent as multipart/form-data.</param>
    /// <param name="version">The version to replace. Omit for the latest cached version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>202 with a summary of the codes now cached; 400 for an unusable CSV; 404 if the ValueSet is not cached or the endpoint is disabled.</returns>
    [HttpPut("value-sets/{id}/codes")]
    [Authorize(CodeUploadPolicy)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    [SwaggerOperation(Summary = "Replace a cached ValueSet's codes from an uploaded CSV (testing only).")]
    [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(ReplaceCodesResponse))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<ReplaceCodesResponse>> ReplaceValueSetCodes(
        [FromRoute] string id,
        [FromForm] IFormFile? file,
        [FromQuery] string? version = null,
        CancellationToken cancellationToken = default) =>
        ReplaceCodesAsync(CodeGroup.CodeGroupTypes.ValueSet, id, file, version, cancellationToken);

    /// <summary>
    /// Replaces the codes of a cached CodeSystem with the contents of an uploaded CSV.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Testing aid, disabled by default.</b> Enabled by <c>Terminology:EnableCodeUploadEndpoint</c>;
    /// while it is false this route reports 404. The replacement lives in the memory of the single
    /// instance that served the request — nothing is written to the terminology data volume, and the
    /// FHIR resource metadata (url, version, id, name, identifiers) is preserved exactly. Revert with
    /// <c>POST /api/terminology/config/$reload-cache</c>, or by restarting the service.
    /// </para>
    /// <para>
    /// <b>Do not enable where more than one instance is running.</b> The upload reaches one instance
    /// only, so subsequent validation calls would be answered inconsistently depending on which
    /// instance handled them, with nothing in the response to indicate it.
    /// </para>
    /// <para>
    /// The CSV <b>must start with a header row</b>, which is skipped without being inspected — columns
    /// are read by position, so a file without one loses its first code. Columns are
    /// <c>code, display</c> plus an optional <c>status</c> of Active or Inactive; codes in a file
    /// without the status column are treated as active.
    /// </para>
    /// </remarks>
    /// <param name="id">The CodeSystem resource id, e.g. "v3-ActCode".</param>
    /// <param name="file">The CSV file, sent as multipart/form-data.</param>
    /// <param name="version">The version to replace. Omit for the latest cached version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>202 with a summary of the codes now cached; 400 for an unusable CSV; 404 if the CodeSystem is not cached or the endpoint is disabled.</returns>
    [HttpPut("code-systems/{id}/codes")]
    [Authorize(CodeUploadPolicy)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    [SwaggerOperation(Summary = "Replace a cached CodeSystem's codes from an uploaded CSV (testing only).")]
    [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(ReplaceCodesResponse))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<ReplaceCodesResponse>> ReplaceCodeSystemCodes(
        [FromRoute] string id,
        [FromForm] IFormFile? file,
        [FromQuery] string? version = null,
        CancellationToken cancellationToken = default) =>
        ReplaceCodesAsync(CodeGroup.CodeGroupTypes.CodeSystem, id, file, version, cancellationToken);

    private async Task<ActionResult<ReplaceCodesResponse>> ReplaceCodesAsync(
        CodeGroup.CodeGroupTypes type,
        string id,
        IFormFile? file,
        string? version,
        CancellationToken cancellationToken)
    {
        if (!terminologyConfig.Value.EnableCodeUploadEndpoint)
        {
            // Report the same 404 an unmapped route would produce, so a disabled instance is
            // indistinguishable from one that never shipped the endpoint.
            return Problem(
                detail: "The requested terminology endpoint was not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                type: "https://tools.ietf.org/html/rfc9110#section-15.5.5");
        }

        // Sanitized the same way the lookups sanitize theirs: the id is matched against cached resource
        // ids, so it must survive the round trip intact.
        var cleanId = SanitizeLookupValue(id);
        // A blank ?version= means "latest", matching GetCodeSystemCode.
        var cleanVersion = string.IsNullOrWhiteSpace(version) ? null : version.Sanitize();
        var cleanFileName = file?.FileName?.SanitizeAndRemove();

        if (string.IsNullOrWhiteSpace(cleanId))
        {
            ModelState.AddModelError("id", $"A {type} 'id' is required.");
        }

        if (file == null || file.Length == 0)
        {
            ModelState.AddModelError("file", "A non-empty CSV file is required.");
        }
        else if (file.Length > MaxCsvUploadBytes)
        {
            ModelState.AddModelError("file", $"The CSV file is too large. The maximum allowed size is {MaxCsvUploadBytes / (1024 * 1024)} MB.");
        }
        else if (!string.IsNullOrEmpty(cleanFileName)
                 && Path.HasExtension(cleanFileName)
                 && !string.Equals(Path.GetExtension(cleanFileName), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            // The part's own content type is not checked: clients disagree about whether a .csv is
            // text/csv, text/plain or application/octet-stream, and content that is not CSV fails to
            // parse below anyway.
            ModelState.AddModelError("file", "The uploaded file must be a .csv file.");
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

        string csvContent;
        using (var reader = new StreamReader(file!.OpenReadStream()))
        {
            csvContent = await reader.ReadToEndAsync(cancellationToken);
        }

        try
        {
            var replaced = cacheService.ReplaceCodesFromCsv(type, cleanId!, cleanVersion, csvContent, cancellationToken);
            var codes = replaced.Codes.Values.SelectMany(c => c).ToList();

            logger.LogInformation(
                "Replaced the codes of {Type} {Id} from uploaded file {FileName}",
                type, cleanId.SanitizeForLog(), cleanFileName ?? "(unnamed)");

            return Accepted(new ReplaceCodesResponse
            {
                Type = type.ToString(),
                Id = replaced.Id ?? cleanId!,
                Version = replaced.Version,
                CodeCount = codes.Count,
                SystemCount = replaced.Codes.Count,
                InactiveCodeCount = codes.Count(c => c switch
                {
                    ValueSetCode valueSetCode => valueSetCode.Status == CodeStatus.Inactive,
                    CodeSystemCode codeSystemCode => codeSystemCode.Status == CodeStatus.Inactive,
                    _ => false
                }),
                FileName = cleanFileName
            });
        }
        catch (CodeGroupNotFoundException ex)
        {
            // Deliberately the narrow type rather than KeyNotFoundException: the message is echoed to the
            // caller, and a dictionary lookup failing anywhere inside the cache is a defect whose message
            // must not be dressed up as a 404. Anything else reaching here falls through to the
            // service-wide handler as a 500 with a traceId.
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound,
                title: $"{type} Not Found",
                type: "https://tools.ietf.org/html/rfc9110#section-15.5.5");
        }
        catch (InvalidOperationException ex)
        {
            // Thrown by Process*Csv for an unsupported column count. The message is a fixed literal
            // describing the expected columns and carries nothing from the request, so it is safe to echo.
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                type: "https://tools.ietf.org/html/rfc9110#section-15.5.1");
        }
        catch (CsvHelperException ex)
        {
            // Deliberately not echoing ex.Message: CsvHelper embeds the offending field's text, which is
            // caller-supplied. The row number is enough to locate the problem.
            var row = ex.Context?.Parser?.Row;
            return Problem(
                detail: row is > 0
                    ? $"The uploaded CSV could not be parsed. The first problem was found at row {row}."
                    : "The uploaded CSV could not be parsed.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                type: "https://tools.ietf.org/html/rfc9110#section-15.5.1");
        }
    }

    #endregion
}