using DataAcquisition.Domain.Application.Serializers;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Extensions;

using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.DataAcquisition.Jobs;
using LantanaGroup.Link.DataAcquisition.Listeners;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Middleware;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.RegisterAll(DataAcquisitionConstants.ServiceName, true);

builder.RegisterQuartzAcquisitionJob(builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.DatabaseConnection));

// Add services to the container.
// Additional configuration is required to successfully run gRPC on macOS.
// For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682
builder.Services.AddControllers(
    options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true
    ).AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.Converters.Add(new QueryPlanConverter());
        options.JsonSerializerOptions.Converters.Add(new QueryPlanPostModelConverter());
        options.JsonSerializerOptions.Converters.Add(new QueryPlanPutModelConverter());
        options.JsonSerializerOptions.Converters.Add(new TimeSpanConverter());
        options.JsonSerializerOptions.ForFhir(ModelInfo.ModelInspector);
    });

builder.RegisterCors();
builder.RegisterHealthChecks();
builder.RegisterSwagger(includeAuth: true);
// Add Link Security
bool allowAnonymousAccess = builder.Configuration.GetValue<bool>("Authentication:EnableAnonymousAccess");
builder.Services.AddLinkBearerServiceAuthentication(options =>
{
    options.Environment = builder.Environment;
    options.AllowAnonymous = allowAnonymousAccess;
    options.Authority = builder.Configuration.GetValue<string>("Authentication:Schemas:LinkBearer:Authority");
    options.ValidateToken = builder.Configuration.GetValue<bool>("Authentication:Schemas:LinkBearer:ValidateToken");
    options.ProtectKey = builder.Configuration.GetValue<bool>("DataProtection:Enabled");
    options.SigningKey = builder.Configuration.GetValue<string>("LinkTokenService:SigningKey");
});

builder.Services.AddTransient<IRetryEntityFactory, RetryEntityFactory>();

builder.RegisterHostedServices((services, settings) =>
{
    if (!settings?.DisableConsumer ?? true)
    {
        services.AddHostedService<DataAcquisitionRequestedListener>();
        services.AddHostedService<PatientCensusScheduledListener>();
    }

    if (!settings?.DisableRetryConsumer ?? true)
    {
        // TODO: Retry consumer services temporarily disabled for LNK-4038
        //services.AddSingleton(new RetryListenerSettings(DataAcquisitionConstants.ServiceName, [KafkaTopic.DataAcquisitionRequestedRetry.GetStringValue(), KafkaTopic.PatientCensusScheduledRetry.GetStringValue()]));
        //services.AddHostedService<RetryListener>();
        //services.AddHostedService<RetryScheduleService>();
    }
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IDataAcquisitionServiceMetrics, DataAcquisitionServiceMetrics>();

var app = builder.Build();

app.ConfigureCommonMiddleware("data");

//check for anonymous access
if (!allowAnonymousAccess)
{
    app.UseAuthentication();
    app.UseMiddleware<UserScopeMiddleware>();
}
app.UseAuthorization();

app.Run();