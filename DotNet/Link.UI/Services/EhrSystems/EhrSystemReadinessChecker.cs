using System.Net.Http.Headers;
using LantanaGroup.Automation;
using LantanaGroup.Automation.Configuration;

namespace Link.UI.Services;

/// <summary>One readiness probe of a named EHR system's FHIR metadata endpoint.</summary>
public sealed record EhrReadinessResult(
    string Name,
    bool Ready,
    int? StatusCode,
    string FhirServerBase,
    string? Error);

/// <summary>
/// Asks each named EHR whether its FHIR metadata endpoint answers.
/// The probe uses the URL Link.UI calls, with that system's auth.
/// </summary>
public sealed class EhrSystemReadinessChecker(IHttpClientFactory httpClientFactory)
{
    public const string HttpClientName = "ehr-system-readiness";
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public Task<EhrReadinessResult> CheckAsync(NamedEhrSystem ehr, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ehr);
        var client = httpClientFactory.CreateClient(HttpClientName);
        return CheckAsync(ehr, client, cancellationToken);
    }

    internal static async Task<EhrReadinessResult> CheckAsync(
        NamedEhrSystem ehr,
        HttpClient client,
        CancellationToken cancellationToken,
        Func<OAuthConfig, string>? bearerToken = null,
        TimeSpan? tokenTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(ehr);
        ArgumentNullException.ThrowIfNull(client);

        var metadata = ehr.FhirServerBase.TrimEnd('/') + "/metadata";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, metadata);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/fhir+json"));
            await ApplyAuthorization(request, ehr, bearerToken, tokenTimeout ?? Timeout, cancellationToken);

            using var response = await client.SendAsync(request, cancellationToken);
            var status = (int)response.StatusCode;
            return new EhrReadinessResult(
                ehr.Name,
                response.IsSuccessStatusCode,
                status,
                ehr.FhirServerBase,
                response.IsSuccessStatusCode ? null : $"FHIR metadata returned HTTP {status}.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new EhrReadinessResult(ehr.Name, false, null, ehr.FhirServerBase, SafeError(ex));
        }
    }

    private static async Task ApplyAuthorization(
        HttpRequestMessage request,
        NamedEhrSystem ehr,
        Func<OAuthConfig, string>? bearerToken,
        TimeSpan tokenTimeout,
        CancellationToken cancellationToken)
    {
        if (ehr.OAuth.ShouldAuthenticate)
        {
            var token = bearerToken is null
                ? await AuthHelper.GetBearerTokenAsync(ehr.OAuth, tokenTimeout, cancellationToken).ConfigureAwait(false)
                : bearerToken(ehr.OAuth);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return;
        }

        if (ehr.BasicAuth.ShouldAuthenticate)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                AuthHelper.GetBasicAuthorization(ehr.BasicAuth));
        }
    }

    private static string SafeError(Exception ex)
    {
        var message = ex.Message ?? "The EHR system could not be reached.";
        return message.Length <= 300 ? message : message[..300];
    }
}
