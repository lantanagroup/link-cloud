using System.Reflection;
using HealthChecks.UI.Client;
using LantanaGroup.Link.DataAcquisition.AcquisitionWorker;
using LantanaGroup.Link.DataAcquisition.AcquisitionWorker.Listeners;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Extensions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Quartz;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Health;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services.Security.Token;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

var consumerSettings = builder.Configuration.GetRequiredSection(nameof(ConsumerSettings)).Get<ConsumerSettings>();

builder.RegisterAll(DataAcquisitionWorkerConstants.ServiceName, true, new List<Func<WebApplicationBuilder, bool>>
{
    new Func<WebApplicationBuilder, bool>(builder => {builder.Services.RegisterQuartzDatabase(builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.DatabaseConnection)); return true; }),
    new Func<WebApplicationBuilder, bool>(builder => {builder.Services.AddSingleton<IKafkaConsumerFactory<string, ReadyToAcquire>, KafkaConsumerFactory<string, ReadyToAcquire>>(); return true; }),
    new Func<WebApplicationBuilder, bool>(builder => {builder.Services.AddSingleton<IKafkaConsumerFactory<string, string>, KafkaConsumerFactory<string, string>>(); return true; }),
    new Func<WebApplicationBuilder, bool>(builder => {builder.Services.AddTransient<IDataAcquisitionServiceMetrics, DataAcquisitionServiceMetrics>(); return true; }),
    new Func<WebApplicationBuilder, bool>(builder => {builder.Services.AddTransient<ICreateSystemToken, CreateSystemToken>(); return true; }),
    new Func<WebApplicationBuilder, bool>(builder => {builder.Services.AddSingleton<TimeProvider>(TimeProvider.System); return true; }),
});

//Add CORS
builder.Services.AddLinkCorsService(options => {
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

// TODO: Retry consumer services temporarily disabled for LNK-4038
if (!consumerSettings?.DisableRetryConsumer ?? true)
{

    //builder.Services.AddSingleton(new RetryListenerSettings(DataAcquisitionWorkerConstants.ServiceName, [KafkaTopic.ReadyToAcquire.GetStringValue()]));
    //builder.Services.AddHostedService<RetryListener>();     
    //builder.Services.AddHostedService<RetryScheduleService>();
}

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

app.UseRouting();
app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
app.MapInfo(Assembly.GetExecutingAssembly(), app.Configuration, "data-worker");
app.ConfigureSwagger();

app.Run();
