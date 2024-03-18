using Azure.Identity;
using HealthChecks.UI.Client;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions;
using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using Serilog.Settings.Configuration;
using System.Diagnostics;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

RegisterServices(builder);
var app = builder.Build();
SetupMiddleware(app);

app.Run();

#region Register Services
static void RegisterServices(WebApplicationBuilder builder)
{
    //load external configuration source if specified
    var externalConfigurationSource = builder.Configuration.GetSection(LinkAdminConstants.AppSettingsSectionNames.ExternalConfigurationSource).Get<string>();
    if (!string.IsNullOrEmpty(externalConfigurationSource))
    {
        switch (externalConfigurationSource)
        {
            case ("AzureAppConfiguration"):
                builder.Configuration.AddAzureAppConfiguration(options =>
                {
                    options.Connect(builder.Configuration.GetConnectionString("AzureAppConfiguration"))
                        // Load configuration values with no label
                        .Select("Link:AdminBFF*", LabelFilter.Null)
                        // Override with any configuration values specific to current hosting env
                        .Select("Link:AdminBFF*", builder.Environment.EnvironmentName);

                    options.ConfigureKeyVault(kv =>
                    {
                        kv.SetCredential(new DefaultAzureCredential());
                    });

                });
                break;
        }
    }

    var serviceInformation = builder.Configuration.GetRequiredSection(LinkAdminConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
    if (serviceInformation != null)
    {
        ServiceActivitySource.Initialize(serviceInformation);
    }
    else
    {
        throw new NullReferenceException("Service Information was null.");
    }

    //configure CORS
    builder.Services.AddCorsService(builder.Environment);    

    //Add problem details
    builder.Services.AddProblemDetails(options => {
        options.CustomizeProblemDetails = ctx =>
        {
            ctx.ProblemDetails.Detail = "An error occured in our API. Please use the trace id when requesting assistence.";
            if (!ctx.ProblemDetails.Extensions.ContainsKey("traceId"))
            {
                string? traceId = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
                ctx.ProblemDetails.Extensions.Add(new KeyValuePair<string, object?>("traceId", traceId));
            }

            if (builder.Environment.IsDevelopment())
            {
                ctx.ProblemDetails.Extensions.Add("API", "Link Administration");
            }
            else
            {
                ctx.ProblemDetails.Extensions.Remove("exception");
            }

        };
    });

    //Add health checks
    builder.Services.AddHealthChecks();

    // Add services to the container.
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        c.IncludeXmlComments(xmlPath);
    });

    // Logging using Serilog
    builder.Logging.AddSerilog();
    var loggerOptions = new ConfigurationReaderOptions { SectionName = LinkAdminConstants.AppSettingsSectionNames.Serilog };
    Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(builder.Configuration, loggerOptions)
                    .Filter.ByExcluding("RequestPath like '/health%'")
                    .Filter.ByExcluding("RequestPath like '/swagger%'")
                    .Enrich.WithExceptionDetails()
                    .Enrich.FromLogContext()
                    .Enrich.WithSpan()
                    .Enrich.With<ActivityEnricher>()
                    .CreateLogger();

    //Serilog.Debugging.SelfLog.Enable(Console.Error); 

    // Add open telemetry
    var telemetryConfig = builder.Configuration.GetSection(LinkAdminConstants.AppSettingsSectionNames.Telemetry).Get<TelemetryConfig>();
    if (telemetryConfig != null)
    {
        builder.Services.AddOpenTelemetryService(telemetryConfig, builder.Environment);
    }
}

#endregion


#region Setup Middleware
static void SetupMiddleware(WebApplication app)
{
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler();
    }

    //configure swagger
    if (app.Configuration.GetValue<bool>(LinkAdminConstants.AppSettingsSectionNames.EnableSwagger))
    {
        var serviceInformation = app.Configuration.GetSection(LinkAdminConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
        app.UseSwagger();
        app.UseSwaggerUI(opts => opts.SwaggerEndpoint("/swagger/v1/swagger.json", serviceInformation != null ? $"{serviceInformation.Name} - {serviceInformation.Version}" : "Link Admin API"));
    }

    app.UseHttpsRedirection();

    app.UseRouting();
    app.UseCors("CorsPolicy");
    app.UseAuthentication();
    //app.UseMiddleware<UserScopeMiddleware>();
    app.UseAuthorization();

    //map health check middleware
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });
}

#endregion



