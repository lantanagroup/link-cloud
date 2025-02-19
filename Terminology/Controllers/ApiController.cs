using Microsoft.AspNetCore.Mvc;
using Terminology.Services;

namespace Terminology.Controllers;

[Route("api")]
public class ApiController(CodeGroupCacheService cacheService, ILogger<ApiController> logger) : Controller
{
    [HttpPost("$reload-cache")]
    public ActionResult ReloadCache()
    {
        logger.LogInformation("Reloading cache");
        cacheService.ClearCache();
        cacheService.LoadCache();
        return NoContent();
    }
}