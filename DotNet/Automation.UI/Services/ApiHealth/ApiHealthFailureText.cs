using Automation.UI.Models.ApiHealth;
using LantanaGroup.Automation.Diagnostics;

namespace Automation.UI.Services.ApiHealth;

/// <summary>
/// Formats an API Health failure with the same block as a FHIR upload failure.
/// Correlation and request ids are "Not provided": this path does not read those headers.
/// </summary>
public static class ApiHealthFailureText
{
    public static string? Format(ApiTestRunResult result)
    {
        if (result.Passed || result.Skipped)
            return null;

        var failure = OperationFailureFactory.FromHttp(new OperationFailureInput
        {
            Name = string.IsNullOrWhiteSpace(result.EndpointName) ? result.EndpointKey : result.EndpointName,
            Step = string.IsNullOrWhiteSpace(result.ServiceName)
                ? "API health check"
                : $"API health check ({result.ServiceName})",
            Method = result.RequestMethod ?? "",
            Endpoint = result.RequestUrl ?? "",
            StatusCode = result.ActualStatusCode,
            ResponseBody = result.ResponseBody,
            TraceIdHeader = result.TraceId,
            RunId = result.RunId == Guid.Empty ? null : result.RunId.ToString("D"),
            RespondingSystem = string.IsNullOrWhiteSpace(result.ServiceName) ? null : result.ServiceName,
            TimestampUtc = result.ExecutedAt
        });

        return failure.FormattedBlock;
    }
}
