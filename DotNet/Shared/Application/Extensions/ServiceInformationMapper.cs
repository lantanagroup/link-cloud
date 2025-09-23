using System.Reflection;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace LantanaGroup.Link.Shared.Application.Extensions;

public static class ServiceInformationMapper
{
    public static void MapInfo(this WebApplication app, Assembly assembly, IConfiguration configuration, string serviceRouteName = "")
    {
        string routePattern = string.IsNullOrEmpty(serviceRouteName) ? "/api/info" : $"/api/{serviceRouteName}/info";
        app.MapGet(routePattern, () => ServiceInformation.GetServiceInformation(assembly, configuration));       
    }
}