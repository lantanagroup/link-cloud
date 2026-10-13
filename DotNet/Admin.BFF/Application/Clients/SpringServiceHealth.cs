using System.Diagnostics;
using System.Text.Json;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;

/// <summary>
/// Reads a Spring Boot service's <c>/health</c> and maps it onto a <see cref="LinkServiceHealthReport"/>.
/// </summary>
/// <remarks>
/// Shared by the Java services' clients, which read the same document. It was duplicated between
/// them, and a duplicate is how a fix lands in one service's row and not the other's.
/// <para>
/// Every report carries at least one entry. The dashboard renders a row per entry and falls back to
/// "No health check entries available" for none, so a service whose health cannot be read has to say
/// so <em>as an entry</em>. Returning an empty report instead showed the service Unhealthy with no
/// explanation, which is what a healthy service with nothing to report would look like -- and it hid
/// the payload the service had actually produced. A slow service is the common case: Spring
/// evaluates health indicators sequentially on the request thread, so one unreachable backend can
/// push a correct answer past this deadline.
/// </para>
/// </remarks>
public static class SpringServiceHealth
{
    private const string HealthUp = "UP";

    /// <summary>The entry a report carries when the service's own health could not be read.</summary>
    public const string ReadEntry = "Health check";

    public static async Task<LinkServiceHealthReport> ReadAsync(
        HttpClient client,
        string service,
        TimeSpan timeout,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        // Timed so the dashboard can show where the budget went. Without it every report carried
        // TotalDuration 00:00:00 and a service answering just inside the deadline looked identical
        // to one answering instantly.
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await client.GetAsync("health", timeoutCts.Token);

            var content = await response.Content.ReadAsStringAsync(timeoutCts.Token);

            HealthResponse? health;

            try
            {
                health = JsonSerializer.Deserialize<HealthResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Failed to deserialize health response from {Service} service", service);

                // Named separately from a timeout: a gateway answering 502 with an HTML body lands
                // here, and the two used to be indistinguishable on the dashboard.
                return Unread(service, stopwatch.Elapsed,
                    $"The service answered {(int)response.StatusCode} with a body that is not a health report.");
            }

            var report = new LinkServiceHealthReport
            {
                Service = service,
                Status = health?.Status?.Equals(HealthUp, StringComparison.OrdinalIgnoreCase) == true
                    ? HealthStatus.Healthy
                    : HealthStatus.Unhealthy,
                TotalDuration = stopwatch.Elapsed
            };

            if (health?.Components is not null)
            {
                foreach (var component in health.Components)
                {
                    report.Entries[component.Key] = new LinkServiceHealthReportEntry
                    {
                        Status = component.Value?.Status?.ToUpperInvariant() == HealthUp
                            ? HealthStatus.Healthy
                            : HealthStatus.Unhealthy,
                        // Spring reports no per-indicator timing, so there is nothing to carry here.
                        Duration = TimeSpan.Zero,
                        Description = component.Value?.Details is { Count: > 0 } details
                            ? JsonSerializer.Serialize(details)
                            : null
                    };
                }
            }

            return report.Entries.Count > 0
                ? report
                // A parsed document with no components at all: the status is real and is kept, but
                // the row still needs something to render.
                : Unread(service, stopwatch.Elapsed, "The service reported no health components.", report.Status);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The dashboard's own request was abandoned. There is no one left to report to, and
            // reporting Unhealthy would record a verdict this check never reached.
            throw;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("{Service} service health check exceeded {Seconds:0.#}s", service, timeout.TotalSeconds);

            return Unread(service, stopwatch.Elapsed,
                $"The service did not answer /health within {timeout.TotalSeconds:0.#}s. Its own /health names the failing backend.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Service} service health check failed", service);

            return Unread(service, stopwatch.Elapsed, "The service's health could not be read.");
        }
    }

    /// <summary>A report for a service whose health could not be read, carrying the reason.</summary>
    private static LinkServiceHealthReport Unread(string service, TimeSpan elapsed, string reason,
        HealthStatus status = HealthStatus.Unhealthy) => new()
        {
            Service = service,
            Status = status,
            TotalDuration = elapsed,
            Entries =
            {
                [ReadEntry] = new LinkServiceHealthReportEntry
                {
                    Status = status,
                    Duration = elapsed,
                    Description = reason
                }
            }
        };
}
