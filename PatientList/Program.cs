using LantanaGroup.Link.PatientList.Services;
using LantanaGroup.Link.Shared.Configs;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

RegisterServices(builder);
var app = builder.Build();
SetupMiddleware(app);

app.Run();

#region Register Services

static void RegisterServices(WebApplicationBuilder builder)
{
    // Additional configuration is required to successfully run gRPC on macOS.
    // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

    // Add services to the container.
    builder.Services.AddGrpc();
    builder.Services.AddGrpcReflection();

    // Logging using Serilog
    builder.Logging.AddSerilog();
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .CreateLogger();

    //Serilog.Debugging.SelfLog.Enable(Console.Error);  

    builder.Services.Configure<KafkaConnection>(builder.Configuration.GetSection("Kafka"));
    builder.Services.Configure<MongoConnection>(builder.Configuration.GetSection("Mongo"));
}

#endregion

#region Set up middleware

static void SetupMiddleware(WebApplication app)
{
    if (app.Configuration.GetValue<bool>("AllowReflection"))
    {
        app.MapGrpcReflectionService();
    }

    // Configure the HTTP request pipeline.
    app.MapGrpcService<PatientListService>();
    app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
}

#endregion
