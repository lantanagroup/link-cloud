using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace LantanaGroup.Link.Shared.Application.Models;

public class ServiceInformation
{
    public static string SectionName = "ServiceInformation";
    public string ServiceName { get; init; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ProductVersion { get; init; } = string.Empty;
    public string Commit { get; init; } = string.Empty;
    public string Build { get; init; } = string.Empty;

    public static ServiceInformation GetServiceInformation(Assembly assembly, IConfiguration configuration)
    {
        var assemblyVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;
        var serviceInformation = configuration
            .GetRequiredSection(SectionName)
            .Get<ServiceInformation>()!;
        
        if (string.IsNullOrEmpty(serviceInformation.Version))
            serviceInformation.Version = assemblyVersion;

        return serviceInformation;
    }
}