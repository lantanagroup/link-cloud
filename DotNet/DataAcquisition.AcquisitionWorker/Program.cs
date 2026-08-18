using HealthChecks.UI.Client;
using LantanaGroup.Link.DataAcquisition.AcquisitionWorker;
using LantanaGroup.Link.DataAcquisition.AcquisitionWorker.Listeners;
using LantanaGroup.Link.DataAcquisition.AcquisitionWorker.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Extensions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Health;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services.Security.Token;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddStandardEnvironmentConfiguration();

builder.RegisterAll(DataAcquisitionWorkerConstants.ServiceName, configureRedis: true);

var consumerSettings = builder.Configuration.GetRequiredSection(nameof(ConsumerSettings)).Get<ConsumerSettings>();

builder.Services.AddTransient<SftpAcquisitionHandler>();

//register worker processor config
builder.Services.Configure<AcquisitionWorkerProcessorSettings>(
    builder.Configuration.GetSection("AcquisitionWorkerProcessorSettings"));

builder.Services.AddTransient<IDataAcquisitionServiceMetrics, DataAcquisitionServiceMetrics>();
builder.Services.AddTransient<ICreateSystemToken, CreateSystemToken>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddSingleton<AcquisitionProcessorBackgroundService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AcquisitionProcessorBackgroundService>());

//Add CORS
builder.Services.AddLinkCorsService(options =>
{
    options.Environment = builder.Environment;
});

builder.Services.AddControllers();
//Add Health Check
var kafkaConnection = builder.Configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>();
var kafkaHealthOptions = new KafkaHealthCheckConfiguration(kafkaConnection, DataAcquisitionConstants.ServiceName).GetHealthCheckOptions();
builder.Services.AddHealthChecks()
        .AddDbContextCheck<DataAcquisitionDbContext>(HealthCheckType.Database.ToString())
        .AddKafka(kafkaHealthOptions, HealthCheckType.Kafka.ToString());

//Add Hosted Services
if (!consumerSettings?.DisableConsumer ?? true)
{
    builder.Services.AddHostedService<ReadyToAcquireListener>();
}

//Add SFTP Acquisition Service
builder.Services.AddHostedService<SftpAcquisitionService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Unlike other services, there are no authentication requirements for the rest api,
    // because it only exposes the /api/.../info and /health endpoints. If other controllers/endpoints
    // are added later, need to add security requirements to this swagger spec.

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
    c.DocumentFilter<HealthChecksFilter>();
});

var app = builder.Build();

// Ensure schema is up to date even when worker starts before API.
app.AutoMigrateEF<DataAcquisitionDbContext>();

app.UseRouting();
app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
app.MapInfo(Assembly.GetExecutingAssembly(), app.Configuration, "data-worker");
app.ConfigureSwagger();

app.Run();
