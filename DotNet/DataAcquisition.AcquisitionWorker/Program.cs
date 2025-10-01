using LantanaGroup.Link.DataAcquisition.AcquisitionWorker;
using LantanaGroup.Link.DataAcquisition.AcquisitionWorker.Listeners;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Quartz;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Services.Security.Token;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.RegisterAll(DataAcquisitionWorkerConstants.ServiceName, true);

builder.Services.RegisterQuartzDatabase(builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.DatabaseConnection));
builder.Services.AddTransient<IDataAcquisitionServiceMetrics, DataAcquisitionServiceMetrics>();
builder.Services.AddTransient<ICreateSystemToken, CreateSystemToken>();
builder.Services.AddSingleton(TimeProvider.System);

builder.RegisterCors();
builder.RegisterHealthChecks();
builder.RegisterSwagger();

builder.Services.AddControllers();

builder.RegisterHostedServices((services, settings) =>
{
    if (!settings?.DisableConsumer ?? true)
    {
        services.AddHostedService<ReadyToAcquireListener>();
    }

    // TODO: Retry consumer services temporarily disabled for LNK-4038
    if (!settings?.DisableRetryConsumer ?? true)
    {
        //services.AddSingleton(new RetryListenerSettings(DataAcquisitionWorkerConstants.ServiceName, [KafkaTopic.ReadyToAcquire.GetStringValue()]));
        //services.AddHostedService<RetryListener>();     
        //services.AddHostedService<RetryScheduleService>();
    }
});

var app = builder.Build();

app.ConfigureCommonMiddleware("data-worker", autoMigrateDb: false);

app.Run();