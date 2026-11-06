namespace Link.UI.Engine.Models;

public class AboutViewModel
{
    public string FhirServerHostname { get; set; } = "Not configured";

    public string? FhirServerBaseUrl { get; set; }
}
