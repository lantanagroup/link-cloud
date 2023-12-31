using Hellang.Middleware.ProblemDetails;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Middleware;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Submission.Application.Models;
using LantanaGroup.Link.Submission.Listeners;
using LantanaGroup.Link.Submission.Settings;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using System.Reflection;
using LantanaGroup.Link.Submission.Application.Interfaces;
using LantanaGroup.Link.Submission.Application.Managers;
using LantanaGroup.Link.Submission.Application.Queries;
using LantanaGroup.Link.Submission.Application.Repositories;
using HealthChecks.UI.Client;

var builder = WebApplication.CreateBuilder(args);

RegisterServices(builder);
var app = builder.Build();
SetupMiddleware(app);

app.Run();

#region Services

static void RegisterServices(WebApplicationBuilder builder)
{
    //Add problem details
    builder.Services.AddProblemDetails(opts => {
        opts.IncludeExceptionDetails = (ctx, ex) => false;
        opts.OnBeforeWriteDetails = (ctx, dtls) =>
        {
            if (dtls.Status == 500)
            {
                dtls.Detail = "An error occured in our API. Please use the trace id when requesting assistence.";
            }
        };
    });

    //Add Settings
    builder.Services.Configure<KafkaConnection>(builder.Configuration.GetRequiredSection(SubmissionConstants.AppSettingsSectionNames.Kafka));
    builder.Services.Configure<MongoConnection>(builder.Configuration.GetRequiredSection(SubmissionConstants.AppSettingsSectionNames.Mongo));
    builder.Services.Configure<SubmissionServiceConfig>(builder.Configuration.GetRequiredSection(nameof(SubmissionServiceConfig)));
    builder.Services.Configure<FileSystemConfig>(builder.Configuration.GetRequiredSection(nameof(FileSystemConfig)));

    // Add services to the container.
    builder.Services.AddHttpClient();
    builder.Services.AddGrpc().AddJsonTranscoding();
    builder.Services.AddGrpcReflection();
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
    builder.Services.AddSingleton<TenantSubmissionRepository>();
    builder.Services.AddTransient<ITenantSubmissionManager, TenantSubmissionManager>();
    builder.Services.AddTransient<ITenantSubmissionQueries, TenantSubmissionQueries>();

    // Add Controllers
    builder.Services.AddControllers();

    // Add hosted services
    builder.Services.AddHostedService<SubmitReportListener>();

    //Add health checks
    builder.Services.AddHealthChecks();

    // Add commands
    // TODO

    // Add queries
    // TODO

    // Add factories
    builder.Services.AddTransient<IKafkaConsumerFactory<SubmitReportKey, SubmitReportValue>, KafkaConsumerFactory<SubmitReportKey, SubmitReportValue>>();

    // Add repositories
    // TODO

    //// Setup CORS
    //builder.Services.AddCors(options =>
    //{
    //    options.AddPolicy("CorsPolicy",
    //        builder => builder
    //            .AllowAnyMethod()
    //            .AllowCredentials()
    //            .SetIsOriginAllowed((host) => true) //lock this down, allows all atm
    //            .AllowAnyHeader());
    //});

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // Logging using Serilog
    builder.Logging.AddSerilog();
    Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(builder.Configuration)
                    .Enrich.WithExceptionDetails()
                    .Enrich.FromLogContext()
                    .Enrich.WithSpan()
                    .Enrich.With<ActivityEnricher>()
                    .CreateLogger();

    // Telemetry Configuration
    // TODO
}

#endregion

#region Midleware

static void SetupMiddleware(WebApplication app)
{
    app.UseProblemDetails();

    //app.UseOpenTelemetryPrometheusScrapingEndpoint();

    // Configure the HTTP request pipeline.
    if (app.Configuration.GetValue<bool>("EnableSwagger"))
    {
        app.UseSwagger();
        app.UseSwaggerUI(opts => opts.SwaggerEndpoint("/swagger/v1/swagger.json", "Submission Service v1"));
    }

    //map health check middleware
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    app.UseRouting();
    app.UseCors("CorsPolicy");
    //app.UseAuthentication();
    app.UseMiddleware<UserScopeMiddleware>();
    //app.UseAuthorization();
    app.UseEndpoints(endpoints => endpoints.MapControllers());
}

#endregion