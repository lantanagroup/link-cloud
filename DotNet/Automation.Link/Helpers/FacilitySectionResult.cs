using LantanaGroup.Link.Sdk.ApiClient;

namespace LantanaGroup.Link.Automation.Link.Helpers;

/// <summary>
/// HTTP outcome of one facility-configuration write. Callers decide whether a failure is a form error or an exception.
/// </summary>
public readonly record struct FacilitySectionResult(bool Success, int StatusCode, string? RawBody, string? TraceId)
{
    public static FacilitySectionResult From(LinkApiResponse response) =>
        new(response.IsSuccessStatusCode, response.StatusCode, response.RawBody, response.TraceId);
}
