using Microsoft.Extensions.Configuration;

namespace LantanaGroup.Link.Shared.Application.Extensions.Logging;

/// <summary>
/// Names the running instance of a service on every log line, so that when a service is scaled out the lines
/// can be told apart: which pod, or which copy under the Aspire AppHost, wrote each one.
/// </summary>
/// <remarks>
/// The services build their Serilog logger from configuration (<c>ReadFrom.Configuration</c>), and Serilog adds
/// every <c>Serilog:Properties</c> entry to every event. So this puts the instance's name there, as
/// <c>ServiceInstance</c>, before the logger is built, and the shared library needs no Serilog reference. It is a
/// property of the event, not part of the message, so nothing that reads the message text sees a change.
/// </remarks>
public static class ServiceInstanceLoggingExtensions
{
    /// <summary>The log event property naming the instance.</summary>
    public const string PropertyName = "ServiceInstance";

    /// <summary>The configuration key Serilog reads the property from.</summary>
    public const string ConfigurationKey = "Serilog:Properties:" + PropertyName;

    /// <summary>
    /// The environment variable a host sets to name the instance itself. The Aspire AppHost sets it on every copy of
    /// a service; the Java services read the same variable.
    /// </summary>
    public const string EnvironmentVariable = "LINK_SERVICE_INSTANCE";

    /// <summary>
    /// Adds <see cref="ConfigurationKey"/> unless it is already configured. Call it before the Serilog logger is built.
    /// </summary>
    public static IConfigurationBuilder AddServiceInstanceLogProperty(this IConfigurationManager configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration[ConfigurationKey]))
        {
            return configuration;
        }

        return configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ConfigurationKey] = ResolveInstanceName(Environment.GetEnvironmentVariable)
        });
    }

    /// <summary>
    /// The instance's name: <see cref="EnvironmentVariable"/> when a host sets it; else <c>HOSTNAME</c>, which
    /// Kubernetes sets to the pod's name (and Docker to the container's); else the machine name.
    /// </summary>
    public static string ResolveInstanceName(Func<string, string?> environment)
    {
        var configured = environment(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        var hostName = environment("HOSTNAME");
        return string.IsNullOrWhiteSpace(hostName) ? Environment.MachineName : hostName.Trim();
    }
}
