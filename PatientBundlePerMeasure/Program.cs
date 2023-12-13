using LantanaGroup.Link.Shared.Configs;
using LantanaGroup.Link.PatientBundlePerMeasure.Listeners;
using PatientBundlePerMeasure.Services;
using System.Text.Json.Serialization;
using Serilog;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;

var builder = WebApplication.CreateBuilder(args);

// Additional configuration is required to successfully run gRPC on macOS.
// For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

builder.Services.Configure<KafkaConnection>(builder.Configuration.GetRequiredSection("KafkaConnection"));

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddControllers(options => { options.ReturnHttpNotAcceptable = true; }).AddXmlDataContractSerializerFormatters().AddJsonOptions(opt => opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddHostedService<PatientNormalizedListener>();

//Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

//map health check middleware
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// Configure the HTTP request pipeline.
app.MapGrpcService<GreeterService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();

