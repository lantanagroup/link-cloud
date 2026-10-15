using Automation.UI.Models;
using LantanaGroup.Link.Automation.Link.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Automation.UI.Controllers;

public class AboutController : Controller
{
    private readonly IOptionsMonitor<AutomationConfig> _automationConfig;

    public AboutController(IOptionsMonitor<AutomationConfig> automationConfig)
    {
        _automationConfig = automationConfig;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var fhirServerBase = _automationConfig.CurrentValue.FhirServerBase;

        var hostname = "Not configured";

        if (!string.IsNullOrWhiteSpace(fhirServerBase)
            && Uri.TryCreate(fhirServerBase, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            hostname = uri.Host;
        }

        var model = new AboutViewModel
        {
            FhirServerHostname = hostname,
            FhirServerBaseUrl = fhirServerBase
        };

        return View(model);
    }
}