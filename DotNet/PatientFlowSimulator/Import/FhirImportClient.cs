using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace LantanaGroup.Link.PatientFlowSimulator.Import;

/// <summary>
/// Posts transaction bundles to a FHIR server, retrying the failures that tend to be transient.
/// </summary>
public sealed class FhirImportClient : IDisposable
{
    private const int MaxAttempts = 3;
    private const string FhirJsonMediaType = "application/fhir+json";

    private readonly HttpClient _http;
    private readonly Action<string> _log;

    public FhirImportClient(string baseUrl, string? bearerToken, string? basicCredentials, Action<string> log)
    {
        _log = log;
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/", UriKind.Absolute),
            Timeout = TimeSpan.FromMinutes(2)
        };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(FhirJsonMediaType));

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }
        else if (!string.IsNullOrWhiteSpace(basicCredentials))
        {
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(basicCredentials));
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encoded);
        }
    }

    /// <summary>
    /// True when the server answers its capability statement.
    /// </summary>
    public async Task<bool> IsReachableAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync("metadata", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>
    /// Posts one transaction bundle to the server base.
    /// </summary>
    public async Task<FhirImportResponse> PostTransactionAsync(string bundleJson, CancellationToken cancellationToken)
    {
        FhirImportResponse? last = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var content = new StringContent(bundleJson, Encoding.UTF8, FhirJsonMediaType);
                using var response = await _http.PostAsync(string.Empty, content, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                last = new FhirImportResponse(response.IsSuccessStatusCode, (int)response.StatusCode, body);

                if (response.IsSuccessStatusCode || !IsRetryable(response.StatusCode))
                {
                    return last;
                }
            }
            catch (HttpRequestException ex)
            {
                last = new FhirImportResponse(false, 0, ex.Message);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                last = new FhirImportResponse(false, 0, $"Timed out: {ex.Message}");
            }

            if (attempt < MaxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _log($"  attempt {attempt} failed ({last.StatusCode} {Truncate(last.Body)}); retrying in {delay.TotalSeconds:F0}s");
                await Task.Delay(delay, cancellationToken);
            }
        }

        return last!;
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();

    private static bool IsRetryable(HttpStatusCode status)
        => status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)status >= 500;

    private static string Truncate(string text)
    {
        var flat = text.Replace("\r", " ").Replace("\n", " ");
        return flat.Length <= 200 ? flat : flat[..200] + "...";
    }
}
