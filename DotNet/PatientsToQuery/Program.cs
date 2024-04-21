using HealthChecks.UI.Client;
using LantanaGroup.Link.PatientsToQuery.Listeners;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using PatientsToQuery.Application.Models;
using PatientsToQuery.Application.Services;
using System.Text.Json.Serialization;
using Serilog;
using PatientsToQuery.Settings;
using Azure.Identity;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using LantanaGroup.Link.Shared.Settings;
using LantanaGroup.Link.Shared.Application.Extensions;
using PatientsToQuery.Application.Interfaces;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;

var builder = WebApplication.CreateBuilder(args);

//load external configuration source if specified
var externalConfigurationSource = builder.Configuration.GetSection(PatientsToQueryConstants.AppSettingsSectionNames.ExternalConfigurationSource).Get<string>();

if (!string.IsNullOrEmpty(externalConfigurationSource))
{
    switch (externalConfigurationSource)
    {
        case ("AzureAppConfiguration"):
            builder.Configuration.AddAzureAppConfiguration(options =>
            {
                options.Connect(builder.Configuration.GetConnectionString("AzureAppConfiguration"))
                        // Load configuration values with no label
                        .Select("*", LabelFilter.Null)
                        // Load configuration values for service name
                        .Select("*", PatientsToQueryConstants.ServiceName)
                        // Load configuration values for service name and environment
                        .Select("*", PatientsToQueryConstants.ServiceName + ":" + builder.Environment.EnvironmentName);

                options.ConfigureKeyVault(kv =>
                {
                    kv.SetCredential(new DefaultAzureCredential());
                });

            });
            break;
    }
}

var serviceInformation = builder.Configuration.GetRequiredSection(PatientsToQueryConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
if (serviceInformation != null)
{
    ServiceActivitySource.Initialize(serviceInformation);
}
else
{
    throw new NullReferenceException("Service Information was null.");
}

// Additional configuration is required to successfully run gRPC on macOS.
// For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
builder.Services.Configure<KafkaConnection>(builder.Configuration.GetRequiredSection(KafkaConstants.SectionName));

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddControllers(options => { options.ReturnHttpNotAcceptable = true; }).AddXmlDataContractSerializerFormatters().AddJsonOptions(opt => opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddHostedService<PatientListener>();

// Logging using Serilog
builder.Logging.AddSerilog();
Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .Filter.ByExcluding("RequestPath like '/health%'")
                .Enrich.WithExceptionDetails()
                .Enrich.FromLogContext()
                .Enrich.WithSpan()
                .Enrich.With<ActivityEnricher>()
                .CreateLogger();


//Add telemetry if enabled
if (builder.Configuration.GetValue<bool>($"{ConfigurationConstants.AppSettings.Telemetry}:EnableTelemetry"))
{
    builder.Services.AddLinkTelemetry(builder.Configuration, options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = PatientsToQueryConstants.ServiceName;
        options.ServiceVersion = serviceInformation.Version; //TODO: Get version from assembly?                
    });

    builder.Services.AddSingleton<IPatientToQueryServiceMetrics, PatientToQueryServiceMetrics>();
}

//Add health checks
builder.Services.AddHealthChecks();


var app = builder.Build();

//map health check middleware
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.Run();
