using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Terminology.Services;

namespace Terminology.Controllers;

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
    public ActionResult ReloadCache()
    {
        logger.LogInformation("Reloading cache");
        cacheService.ClearCache();
        cacheService.LoadCache();
        return NoContent();
    }
}