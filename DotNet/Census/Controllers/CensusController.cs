using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.Census.Controllers;

[Route("api/census/{facilityId}")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[ApiController]
public class CensusController : Controller
{
    private readonly ILogger<CensusController> _logger;
    public CensusController(ILogger<CensusController> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    
}