using Azure.Identity;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

//load external configuration source if specified
var externalConfigurationSource = builder.Configuration.GetSection(LinkAdminConstants.AppSettingsSectionNames.ExternalConfigurationSource).Get<string>();
if (!string.IsNullOrEmpty(externalConfigurationSource))
{
    switch (externalConfigurationSource)
    {
        case ("AzureAppConfiguration"):
            builder.Configuration.AddAzureAppConfiguration(options =>
            {
                options.Connect(builder.Configuration.GetConnectionString("AzureAppConfiguration"))
                    // Load configuration values with no label
                    .Select("Link:AdminBFF*", LabelFilter.Null)
                    // Override with any configuration values specific to current hosting env
                    .Select("Link:AdminBFF*", builder.Environment.EnvironmentName);

                options.ConfigureKeyVault(kv =>
                {
                    kv.SetCredential(new DefaultAzureCredential());
                });

            });
            break;
    }
}

var serviceInformation = builder.Configuration.GetRequiredSection(LinkAdminConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
if (serviceInformation != null)
{
    ServiceActivitySource.Initialize(serviceInformation);
}
else
{
    throw new NullReferenceException("Service Information was null.");
}

//Add problem details
builder.Services.AddProblemDetails(options => {
    options.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Detail = "An error occured in our API. Please use the trace id when requesting assistence.";
        if (!ctx.ProblemDetails.Extensions.ContainsKey("traceId"))
        {
            string? traceId = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
            ctx.ProblemDetails.Extensions.Add(new KeyValuePair<string, object?>("traceId", traceId));
        }

        if (builder.Environment.IsDevelopment())
        {
            ctx.ProblemDetails.Extensions.Add("API", "Link Administration");
        }
        else
        {
            ctx.ProblemDetails.Extensions.Remove("exception");
        }

    };
});

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();



//app.MapGet("/weatherforecast", () =>
//{
//    var forecast = Enumerable.Range(1, 5).Select(index =>
//        new WeatherForecast
//        (
//            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
//            Random.Shared.Next(-20, 55),
//            summaries[Random.Shared.Next(summaries.Length)]
//        ))
//        .ToArray();
//    return forecast;
//})
//.WithName("GetWeatherForecast")
//.WithOpenApi();

app.Run();

