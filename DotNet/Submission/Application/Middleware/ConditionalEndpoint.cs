using System.Reflection;
using LantanaGroup.Link.Submission.Controllers;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace LantanaGroup.Link.Submission.Application.Middleware;

public class ConditionalEndpoint(RequestDelegate next, IConfiguration configuration, ILogger<ConditionalEndpoint> logger)
{
    private async Task SetResponseNotEnabled(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsync("This feature is disabled.");
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        // Access the matched endpoint after routing
        var endpoint = context.GetEndpoint();

        // Check if an endpoint was matched
        if (endpoint != null)
        {
            // Perform specific type-safe checks using endpoint metadata
            var metadata = endpoint.Metadata;
            var controllerActionDescriptor = metadata.GetMetadata<ControllerActionDescriptor>();

            if (controllerActionDescriptor != null)
            {
                // SubmissionController
                if (controllerActionDescriptor.ControllerTypeInfo == typeof(SubmissionController))
                {
                    var downloadReportEnabled = configuration.GetValue<bool>("Features:DownloadReportEnabled");

                    // DownloadReport
                    if (controllerActionDescriptor.MethodInfo.Name == "DownloadReport" && !downloadReportEnabled)
                    {
                        logger.LogWarning("Request to download report is rejected due to configuration.");
                        await this.SetResponseNotEnabled(context);
                    }
                }
            }
        }

        await next(context);
    }
}