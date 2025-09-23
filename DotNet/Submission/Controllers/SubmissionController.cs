using System.Net.Http.Headers;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Submission.Application.Config;
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
    BlobStorageService blobStorageService) : Controller
{
    /**
     * Downloads the specified report's data as a ZIP archive
     * <param name="facilityId">The ID of the facility</param>
     * <param name="reportId">The ID of the report to download</param>
     * <remarks>Gets information about the report from the report service in order to construct the directory/path for the report.</remarks>
     */
    [HttpGet("{facilityId}/{reportId}")]
    public async Task<IActionResult> DownloadReport([FromRoute] string facilityId, [FromRoute] string reportId)
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
        
        string reportUrl = $"{serviceRegistry.Value.ReportServiceApiUrl.TrimEnd('/')}/Report/summaries/{sanitizedFacilityId}?reportId={sanitizedReportId.SanitizeAndRemove()}";
        var reportResponse = await client.GetAsync(reportUrl);

        if (!reportResponse.IsSuccessStatusCode)
        {
            logger.LogError($"Report service return {reportResponse.StatusCode} for {reportUrl.Sanitize()}: {reportResponse.ReasonPhrase.Sanitize()}");
            return StatusCode((int)reportResponse.StatusCode, "Unable to retrieve report metadata.");
        }
        
        var jsonResponse = System.Text.Json.JsonDocument.Parse(
            await reportResponse.Content.ReadAsStringAsync());

        if (!jsonResponse.RootElement.TryGetProperty("payloadRootUri", out var payloadRootUri) ||
            payloadRootUri.GetString() == null)
        {
            logger.LogError("Missing 'payloadRootUri' in the response.");
            throw new Exception("Missing 'payloadRootUri' in the response.");
        }

        IDictionary<string, byte[]> files = await blobStorageService.DownloadFromExternalAsync(payloadRootUri.GetString());

        // TODO: Consider changing this to store the ZIP on disk, instead, and check if the ZIP already exists
        var compressedData = this.CompressFiles(files);
        
        return File(compressedData, "application/zip", $"{sanitizedReportId}.zip");
    }
    
    /**
     * Compresses the contents of the specified files into a ZIP archive (in memory) and returns it as a byte array.
     * <returns>A byte array containing the compressed data of the specified files as a ZIP archive.</returns>
     */
    private byte[] CompressFiles(IDictionary<string, byte[]> files)
    {
        using var memoryStream = new MemoryStream();
        using (var zipArchive =
               new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create,
                   true))
        {
            foreach (var file in files)
            {
                // Add each file to the zip archive
                var zipEntry = zipArchive.CreateEntry(file.Key, System.IO.Compression.CompressionLevel.Optimal);
                using var zipEntryStream = zipEntry.Open();
                new MemoryStream(file.Value).CopyTo(zipEntryStream);
            }
        }

        // Return the ZIP archive as a byte array
        return memoryStream.ToArray();
    }
}