using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;
using Serilog.Exceptions;

namespace Automation.UI.Services;

/// <summary>
/// Serilog setup copied from the other .NET services. An empty Grafana Loki URI is a no-op
/// so a local run still starts. The sink is enabled only when some GrafanaLoki entry has a URI,
/// which is how App Configuration and compose already point the other services at Loki.
/// </summary>
public static class AutomationSerilog
{
    public static bool HasLokiSink(IConfiguration configuration)
    {
        foreach (var child in configuration.GetSection("Serilog:WriteTo").GetChildren())
        {
            if (!string.Equals(child["Name"], "GrafanaLoki", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrWhiteSpace(child["Args:uri"]))
                return true;
        }

        return false;
    }

    public static void Use(WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();

        LoggerConfiguration loggerConfiguration;
        if (HasLokiSink(builder.Configuration))
        {
            loggerConfiguration = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration);
        }
        else
        {
            loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .WriteTo.Console();
        }

        Log.Logger = loggerConfiguration
            .Filter.ByExcluding("RequestPath like '/health%'")
            .Filter.ByExcluding("RequestPath like '/swagger%'")
            .Enrich.WithExceptionDetails()
            .Enrich.FromLogContext()
            .Enrich.WithSpan()
            .Enrich.With<ActivityEnricher>()
            .CreateLogger();

        builder.Logging.AddSerilog(Log.Logger, dispose: true);
    }
}
