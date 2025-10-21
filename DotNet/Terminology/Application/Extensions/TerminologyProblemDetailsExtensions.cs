using LantanaGroup.Link.Terminology.Application.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Net;

namespace LantanaGroup.Link.Terminology.Application.Extensions;

internal static class TerminologyProblemDetailsExtensions
{
    internal static IServiceCollection AddTerminologyProblemDetails(
        this IServiceCollection services,
        IWebHostEnvironment environment,
        bool includeExceptionDetails = false)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = ctx =>
            {
                var statusCode = ctx.ProblemDetails.Status ?? ctx.HttpContext.Response.StatusCode;

                if (statusCode == (int)HttpStatusCode.InternalServerError)
                {
                    ctx.ProblemDetails.Detail =
                        "An error occurred in our API. Please use the trace id when requesting assistance.";
                }
                else if (string.IsNullOrWhiteSpace(ctx.ProblemDetails.Detail))
                {
                    ctx.ProblemDetails.Detail = statusCode == (int)HttpStatusCode.NotFound
                        ? "The requested terminology endpoint was not found."
                        : "The request could not be completed. Please use the trace id when requesting assistance.";
                }

                if (!ctx.ProblemDetails.Extensions.ContainsKey("traceId"))
                {
                    var traceId = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
                    ctx.ProblemDetails.Extensions.Add(new KeyValuePair<string, object?>("traceId", traceId));
                }

                if (environment.IsDevelopment() || includeExceptionDetails)
                {
                    ctx.ProblemDetails.Extensions.Add("API", TerminologyConstants.ServiceName);
                }
                else
                {
                    ctx.ProblemDetails.Extensions.Remove("exception");
                }
            };
        });

        return services;
    }
}
