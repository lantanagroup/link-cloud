using System.Net.Http.Headers;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
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
    IOptions<ServiceRegistry> serviceRegistry) : Controller
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
        string sanitizedFacilityId = facilityId
            .Replace("\n", "")
            .Replace("\r", "")
            .Replace("\\", "")
            .Replace("/", "")
            .Replace("..", "");
        
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
        
        string reportUrl = $"{serviceRegistry.Value.ReportServiceApiUrl.TrimEnd('/')}/Report/summaries/{sanitizedFacilityId}?reportId={reportId}";
        var reportResponse = await client.GetAsync(reportUrl);
        var jsonResponse = System.Text.Json.JsonDocument.Parse(
            await reportResponse.Content.ReadAsStringAsync());
        
        if (!jsonResponse.RootElement.TryGetProperty("reportStartDate", out var reportStartDateElement) ||
            !jsonResponse.RootElement.TryGetProperty("reportEndDate", out var reportEndDateElement))
        {
            logger.LogError("Missing 'reportStartDate' or 'reportEndDate' in the response.");
            throw new Exception("Missing 'reportStartDate' or 'reportEndDate' in the response.");
        }
        
        if (!jsonResponse.RootElement.TryGetProperty("reportTypes", out var reportTypesElement) || 
            reportTypesElement.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            logger.LogError("Missing 'reportTypes' or it is not an array in the response.");
            throw new Exception("Missing 'reportTypes' or it is not an array in the response.");
        }
        
        var reportTypes = reportTypesElement.EnumerateArray()
            .Select(type => type.GetString())
            .Cast<string>().ToList();
        
        DateTime reportStartDate = reportStartDateElement.GetDateTime();
        DateTime reportEndDate = reportEndDateElement.GetDateTime();
        string reportDirectoryName =
            pathNamingService.GetSubmissionDirectoryName(sanitizedFacilityId, reportTypes, reportStartDate, reportEndDate,
                reportId);
        string fullPath = Path.Join(config.Value.SubmissionDirectory, reportDirectoryName);
        string normalizedBaseDirectory = Path.GetFullPath(config.Value.SubmissionDirectory);
        string normalizedFullPath = Path.GetFullPath(fullPath);
        
        if (!normalizedFullPath.StartsWith(normalizedBaseDirectory + Path.DirectorySeparatorChar))
        {
            logger.LogError("Attempted access outside of base directory: {FullPath}", fullPath);
            return BadRequest("Invalid path.");
        }

        // TODO: Consider changing this to store the ZIP on disk, instead, and check if the ZIP already exists
        var compressedData = this.CompressDirectory(normalizedFullPath);
        
        return File(compressedData, "application/zip", $"{reportDirectoryName}.zip");
    }
    
    /**
     * Compresses the contents of a specified directory into a ZIP archive (in memory) and returns it as a byte array.
     * <param name="directory">The path to the directory that needs to be compressed. The directory must exist; otherwise, an exception is thrown.</param>
     * <returns>A byte array containing the compressed data of the specified directory as a ZIP archive.</returns>
     * <exception cref="DirectoryNotFoundException">Thrown when the specified directory does not exist.</exception>
     */
    private byte[] CompressDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"The directory '{directory}' does not exist.");
        }

        using var memoryStream = new MemoryStream();
        using (var zipArchive =
               new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create,
                   true))
        {
            var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                // Retrieve the relative path to preserve the folder structure inside the zip
                var relativePath = Path.GetRelativePath(directory, file);

                // Add each file to the zip archive
                var zipEntry = zipArchive.CreateEntry(relativePath, System.IO.Compression.CompressionLevel.Optimal);
                using var zipEntryStream = zipEntry.Open();
                using var fileStream = System.IO.File.OpenRead(file);
                fileStream.CopyTo(zipEntryStream);
            }
        }

        // Return the ZIP archive as a byte array
        return memoryStream.ToArray();
    }
}