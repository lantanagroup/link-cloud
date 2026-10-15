namespace Automation.UI.Models;

public class AboutViewModel
{
    public string FhirServerHostname { get; set; } = "Not configured";

    public string? FhirServerBaseUrl { get; set; }
}
