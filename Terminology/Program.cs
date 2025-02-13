using System.Reflection;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using Serilog.Settings.Configuration;
using Terminology.Application.Formatters;
using Terminology.Application.Settings;
using Terminology.Services;

static void RegisterServices(WebApplicationBuilder builder)
{
    var serviceInformation = builder.Configuration.GetRequiredSection(TerminologyConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
    
    builder.Services.AddHttpClient();

    builder.Services.AddControllers(options =>
    {
        options.ModelBinderProviders.Insert(0, new FhirModelBinderProvider());
        options.OutputFormatters.Insert(0, new FhirOutputFormatter());
    });

    builder.Services.AddHealthChecks();

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        c.IncludeXmlComments(xmlPath);
    });

    ConfigureLogging(builder);

    //Add CORS
    builder.Services.AddLinkCorsService(options => { 
        options.Environment = builder.Environment;
    });            

    //Add telemetry if enabled
    builder.Services.AddLinkTelemetry(builder.Configuration, options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = TerminologyConstants.ServiceName;
        options.ServiceVersion = serviceInformation?.Version; //TODO: Get version from assembly?                
    });
    
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<CodeGroupCacheService>();
    
    builder.Services.Configure<TerminologyConfig>(builder.Configuration.GetSection(TerminologyConstants.AppSettingsSectionNames.Terminology));

    builder.Services.AddHostedService<Startup>();
}

static void ConfigureLogging(WebApplicationBuilder builder)
{
    // Logging using Serilog
    builder.Logging.AddSerilog();
    var loggerOptions = new ConfigurationReaderOptions { SectionName = TerminologyConstants.AppSettingsSectionNames.Serilog };
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration, loggerOptions)
        .Filter.ByExcluding("RequestPath like '/health%'")
        .Filter.ByExcluding("RequestPath like '/swagger%'")
        .Enrich.WithExceptionDetails()
        .Enrich.FromLogContext()
        .Enrich.WithSpan()
        .Enrich.With<ActivityEnricher>()
        .Enrich.FromLogContext()
        .CreateLogger();
            
    Serilog.Debugging.SelfLog.Enable(Console.Error);
}

static void SetupMiddleware(WebApplication app)
{
    // Configure the HTTP request pipeline.
    app.ConfigureSwagger();
    
    app.UseRouting();            
    app.UseCors(CorsSettings.DefaultCorsPolicyName);
    
    app.MapControllers();
}

var builder = WebApplication.CreateBuilder(args);

RegisterServices(builder);
var app = builder.Build();
SetupMiddleware(app);
app.Run();