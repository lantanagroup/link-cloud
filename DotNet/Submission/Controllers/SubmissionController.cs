using System.Net.Http.Headers;
using System.Text.Json;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Submission.Application.Config;
using LantanaGroup.Link.Submission.Application.Interfaces;
using LantanaGroup.Link.Submission.Application.Services;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Submission.Controllers;

[Route("api/[controller]")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[ApiController]
public class SubmissionController(
    ILogger<SubmissionController> logger,
    IOptions<SubmissionServiceConfig> config,
    PathNamingService pathNamingService,
    IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> linkBearerServiceOptions,
    IOptions<LinkTokenServiceSettings> tokenServiceSettings,
    ICreateSystemToken createSystemToken,
    IHttpClientFactory httpClientFactory,
    IOptions<ServiceRegistry> serviceRegistry,
    IStorageService blobStorageService) : Controller
{
    /**
     * Downloads the specified report's data as a ZIP archive
     * <param name="facilityId">The ID of the facility</param>
     * <param name="reportId">The ID of the report to download</param>
     * <param name="external">Whether to download from external or internal (default) storage</param>
     * <param name="cancellationToken">Token that cancels the metadata read and the ZIP write.</param>
     * <remarks>Gets information about the report from the report service in order to construct the directory/path for the report.</remarks>
     */
    [HttpGet("{facilityId}/{reportId}")]
    public async Task<IActionResult> DownloadReport(
        [FromRoute] string facilityId,
        [FromRoute] string reportId,
        [FromQuery] bool external = false,
        CancellationToken cancellationToken = default)
    {
        string sanitizedFacilityId = facilityId.SanitizeAndRemove();

        if (string.IsNullOrWhiteSpace(sanitizedFacilityId))
        {
            return BadRequest("facilityId must not be null, empty, or white space");
        }

        var sanitizedReportId = reportId.SanitizeAndRemove();

        if (string.IsNullOrWhiteSpace(sanitizedReportId))
        {
            return BadRequest("ReportId must not be null, empty, or white space");
        }

        if (string.IsNullOrEmpty(serviceRegistry.Value?.ReportServiceApiUrl))
        {
            logger.LogError("Report Service API Url is missing from Service Registry.");
            throw new Exception("Report Service API Url is missing from Service Registry.");
        }

        HttpClient client = httpClientFactory.CreateClient();

        if (!linkBearerServiceOptions.Value.AllowAnonymous)
        {
            if (tokenServiceSettings.Value.SigningKey is null)
                throw new Exception("Link Token Service Signing Key is missing.");

            //Add link token
            var token = await createSystemToken.ExecuteAsync(tokenServiceSettings.Value.SigningKey, 5);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        string reportUrl = $"{serviceRegistry.Value.ReportServiceApiUrl.TrimEnd('/')}/schedules/{sanitizedReportId.SanitizeAndRemove()}";
        using var reportResponse = await client.GetAsync(reportUrl, cancellationToken);

        if (!reportResponse.IsSuccessStatusCode)
        {
            logger.LogError("Report service return {StatusCode} for {ReportUrl}: {ReasonPhrase}", reportResponse.StatusCode, reportUrl.Sanitize(), reportResponse.ReasonPhrase.Sanitize());
            return StatusCode((int)reportResponse.StatusCode, "Unable to retrieve report metadata.");
        }

        using var jsonResponse = JsonDocument.Parse(
            await reportResponse.Content.ReadAsStringAsync(cancellationToken));

        if (!jsonResponse.RootElement.TryGetProperty("payloadRootUri", out var payloadRootUri) ||
            payloadRootUri.GetString() == null)
        {
            logger.LogError("Missing 'payloadRootUri' in the response.");
            throw new Exception("Missing 'payloadRootUri' in the response.");
        }

        var payloadRoot = payloadRootUri.GetString()!;
        var reportTypes = new List<string>();
        if (jsonResponse.RootElement.TryGetProperty("reportTypes", out var reportTypesElement))
        {
            foreach (JsonElement reportTypeElement in reportTypesElement.EnumerateArray())
            {
                string? reportType = reportTypeElement.GetString();
                if (reportType != null)
                {
                    reportTypes.Add(reportType);
                }
            }
        }

        // Metadata is loaded before the body starts, so a missing container still
        // returns an error status. The ZIP itself is written one blob at a time.
        if (external)
        {
            if (!blobStorageService.HasExternalClient())
                throw new InvalidOperationException("Not configured for external blob storage.");
        }
        else if (!blobStorageService.HasInternalClient())
        {
            throw new InvalidOperationException("Not configured for internal blob storage.");
        }

        return new ZipAttachmentResult($"{sanitizedReportId}.zip", (body, token) =>
            external
                ? blobStorageService.WriteExternalAsZipAsync(reportTypes, payloadRoot, body, token)
                : blobStorageService.WriteInternalAsZipAsync(payloadRoot, body, token));
    }

    /// <summary>
    /// Sends a ZIP attachment by writing it straight to the response body.
    /// </summary>
    private sealed class ZipAttachmentResult : IActionResult
    {
        private readonly string _fileName;
        private readonly Func<Stream, CancellationToken, Task> _write;

        public ZipAttachmentResult(string fileName, Func<Stream, CancellationToken, Task> write)
        {
            _fileName = fileName;
            _write = write;
        }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;
            response.StatusCode = StatusCodes.Status200OK;
            response.ContentType = "application/zip";
            response.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = _fileName
            }.ToString();

            // ZipArchive finalizes with a synchronous write. Build the file first,
            // then copy it to the response so Kestrel never sees that write.
            await SubmissionZipResponse.CopyToAsync(response.Body, _write, context.HttpContext.RequestAborted);
        }
    }
}
