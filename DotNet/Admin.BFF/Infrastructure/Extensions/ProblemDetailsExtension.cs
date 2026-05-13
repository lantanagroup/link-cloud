using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using System.Diagnostics;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions
{
    public static class ProblemDetailsExtension
    {
        private const string UserFacingKey = "userFacing";

        /// <summary>
        /// Creates a Problem result whose detail message is shown to the user even for 500 errors,
        /// bypassing the generic "An error occurred" override. Use when the message is safe and
        /// meaningful to display.
        /// </summary>
        public static IResult UserFacingProblem(string detail, int statusCode) =>
            Results.Problem(detail, statusCode: statusCode,
                extensions: new Dictionary<string, object?> { [UserFacingKey] = true });

        public static IServiceCollection AddBffProblemDetailsService(this IServiceCollection services, Action<ProblemDetailsOptions>? options = null)
        {
            var problemDetailsOptions = new ProblemDetailsOptions();
            options?.Invoke(problemDetailsOptions);

            services.AddProblemDetails(options =>
            {
                options.CustomizeProblemDetails = ctx =>
                {
                    // Remove the userFacing marker — it must not appear in the response body
                    bool isUserFacing = ctx.ProblemDetails.Extensions.Remove(UserFacingKey, out _);

                    // For 500 errors, replace detail with a generic message unless explicitly user-facing
                    if (ctx.ProblemDetails.Status == StatusCodes.Status500InternalServerError && !isUserFacing)
                    {
                        ctx.ProblemDetails.Detail = "An error occured in our API. Please use the trace id when requesting assistance.";
                    }
                    else if (string.IsNullOrEmpty(ctx.ProblemDetails.Detail))
                    {
                        ctx.ProblemDetails.Detail = "An error occured in our API. Please use the trace id when requesting assistance.";
                    }

                    if (!ctx.ProblemDetails.Extensions.ContainsKey("traceId"))
                    {
                        string? traceId = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
                        ctx.ProblemDetails.Extensions.Add(new KeyValuePair<string, object?>("traceId", traceId));
                    }

                    if (problemDetailsOptions.Environment.IsDevelopment() || problemDetailsOptions.IncludeExceptionDetails)
                    {
                        ctx.ProblemDetails.Extensions.Add("API", problemDetailsOptions.ServiceName);
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
        public string ServiceName { get; set; } = null!;
        public bool IncludeExceptionDetails { get; set; } = false;
    }
}
