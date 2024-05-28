using System.Reflection;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace LantanaGroup.Link.Shared.Application.Extensions;

public static class ConfigureSwaggerExtension
{
    public static void ConfigureSwagger(this WebApplication app, Action<SwaggerOptions> specAction = null, Action<SwaggerUIOptions> uiAction = null)
    {
        if (!app.Configuration.GetValue<bool>(ConfigurationConstants.AppSettings.EnableSwagger, false))
            return;
        
        var serviceInformation = app.Configuration
            .GetSection(ConfigurationConstants.AppSettings.ServiceInformation)
            .Get<ServiceInformation>();
        
        app.UseSwagger(opts =>
        {
            specAction?.Invoke(opts);
        });
        
        app.UseSwaggerUI(opts =>
        {
            opts
                .SwaggerEndpoint("/swagger/v1/swagger.json",
                    serviceInformation != null
                        ? $"{serviceInformation.Name} - {serviceInformation.Version}"
                        : "Link " + Assembly.GetExecutingAssembly().GetName() + " Service");
            
            uiAction?.Invoke(opts);
        });
    }
}