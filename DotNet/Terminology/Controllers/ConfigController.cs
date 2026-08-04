using System.Net;
using CsvHelper;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Terminology.Application.Interfaces;
using LantanaGroup.Link.Terminology.Application.Models;
using LantanaGroup.Link.Terminology.Application.Settings;
using LantanaGroup.Link.Terminology.Services;
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
    IOptions<TerminologyConfig> terminologyConfig,
    ILogger<ConfigController> logger) : Controller
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

    #region Code upload (non-production)

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
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    [SwaggerOperation(Summary = "Replace a cached ValueSet's codes from an uploaded CSV (testing only).")]
    [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(ReplaceCodesResponse))]
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
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    [SwaggerOperation(Summary = "Replace a cached CodeSystem's codes from an uploaded CSV (testing only).")]
    [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(ReplaceCodesResponse))]
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

        var cleanId = id?.Sanitize();
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
            var replaced = cacheService.ReplaceCodesFromCsv(type, cleanId!, cleanVersion, csvContent);
            var codes = replaced.Codes.Values.SelectMany(c => c).ToList();

            logger.LogInformation(
                "Replaced the codes of {Type} {Id} from uploaded file {FileName}",
                type, cleanId, cleanFileName ?? "(unnamed)");

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
        catch (KeyNotFoundException ex)
        {
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