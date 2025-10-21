namespace LantanaGroup.Automation.Helpers;

/// <summary>
/// Generic interface for scraping logs from a centralized logging system.
/// Implementations can target Loki, Elasticsearch, Azure Monitor, etc.
/// </summary>
public interface ILogScraper
{
    /// <summary>
    /// Scrapes error logs since the last call, optionally scoped by correlation context.
    /// </summary>
    Task ScrapeErrorsAsync(string? correlationId1 = null, string? correlationId2 = null);

    /// <summary>
    /// Scrapes all services for an error summary over the given lookback window.
    /// </summary>
    Task ScrapeAllServicesErrorSummaryAsync(TimeSpan lookback, string? correlationId1 = null, string? correlationId2 = null);

    /// <summary>
    /// Scrapes a specific service component's history for a given lookback window.
    /// </summary>
    Task ScrapeServiceHistoryAsync(string componentName, TimeSpan lookback, string label);

    /// <summary>
    /// Returns exception/error log lines for a specific service component.
    /// </summary>
    Task<List<string>> GetServiceExceptionLinesAsync(string componentName, TimeSpan lookback, int limit = 20, string? correlationId1 = null, string? correlationId2 = null);

    /// <summary>
    /// Returns a human-readable summary of recent activity for a named service, or null if no activity.
    /// </summary>
    Task<string?> GetServiceActivitySummaryAsync(string componentName, TimeSpan lookback);
}
