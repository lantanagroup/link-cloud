using DataAcquisition.Domain.Extensions;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Listeners;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services;
using DataAcquisition.AcquisitionWorker.Listeners;
using LantanaGroup.Link.Shared.Application.Utilities;
using DataAcquisition.AcquisitionWorker;

var builder = WebApplication.CreateBuilder(args);

var consumerSettings = builder.Configuration.GetRequiredSection(nameof(ConsumerSettings)).Get<ConsumerSettings>();

builder.RegisterAll(DataAcquisitionWorkerConstants.ServiceName);

//Add Hosted Services
if (!consumerSettings?.DisableConsumer ?? true)
{
    builder.Services.AddHostedService<ReadyToAcquireListener>();
}

if (!consumerSettings?.DisableRetryConsumer ?? true)
{

    builder.Services.AddSingleton(new RetryListenerSettings(DataAcquisitionWorkerConstants.ServiceName, [KafkaTopic.ReadyToAcquire.GetStringValue()]));
    builder.Services.AddHostedService<RetryListener>();
    builder.Services.AddHostedService<RetryScheduleService>();
}

var app = builder.Build();

app.UseHttpsRedirection();
app.Run();
