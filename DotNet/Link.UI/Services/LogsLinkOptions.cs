namespace Link.UI.Services;

/// <summary>
/// Optional browser links for the logs area. Empty values mean the tool is not published here.
/// These are not service URLs and are not read from the configuration catalog.
/// </summary>
public sealed class LogsLinkOptions
{
    public const string SectionName = "LinkUi";

    public string? KafkaUiUrl { get; set; }
    public string? GrafanaUrl { get; set; }
}
