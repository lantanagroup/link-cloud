using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using System.Diagnostics;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions
{
    public static class ProblemDetailsExtension
    {
        public static IServiceCollection AddProblemDetailsService(this IServiceCollection services, Action<ProblemDetailsOptions>? options = null)
        {
            var problemDetailsOptions = new ProblemDetailsOptions();
            options?.Invoke(problemDetailsOptions);

            services.AddProblemDetails(options => {
                options.CustomizeProblemDetails = ctx =>
                {                    
                    ctx.ProblemDetails.Detail = "An error occured in our API. Please use the trace id when requesting assistence.";
                    if (!ctx.ProblemDetails.Extensions.ContainsKey("traceId"))
                    {
                        string? traceId = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
                        ctx.ProblemDetails.Extensions.Add(new KeyValuePair<string, object?>("traceId", traceId));
                    }

                    if (problemDetailsOptions.Environment.IsDevelopment() || problemDetailsOptions.IncludeExceptionDetails)
                    {
                        ctx.ProblemDetails.Extensions.Add("API", LinkAdminConstants.ServiceName);
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

    public class ProblemDetailsOptions
    {
        public IWebHostEnvironment Environment { get; set; } = null!;
        public bool IncludeExceptionDetails { get; set; } = false;
    }
}
