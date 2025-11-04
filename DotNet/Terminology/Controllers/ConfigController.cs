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
public class ConfigController(CodeGroupCacheService cacheService, ILogger<ConfigController> logger) : Controller
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
}