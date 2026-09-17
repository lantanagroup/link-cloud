using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Results;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services
{
    public class ValidateFhirServerConnectionRequest
    {
        public string? FhirServerUrl { get; set; }
    }

    public interface IValidateFhirServerConnectionService
    {
        Task<FhirServerConnectionResult> ValidateConnection(ValidateFhirServerConnectionRequest request,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Validates that Link can reach a FHIR server using nothing but its base URL, by requesting the
    /// server's /metadata endpoint and confirming a CapabilityStatement comes back. Unlike
    /// <see cref="IValidateFacilityConnectionService"/> this requires no persisted facility
    /// configuration and performs no authentication, so it can be used during onboarding before any
    /// facility configuration exists.
    /// </summary>
    public class ValidateFhirServerConnectionService : IValidateFhirServerConnectionService
    {
        /// <summary>
        /// Upper bound on how long we will wait for a FHIR server to answer /metadata before
        /// declaring it unreachable. Deliberately short: this endpoint backs an interactive
        /// onboarding form.
        /// </summary>
        private static readonly TimeSpan MetadataTimeout = TimeSpan.FromSeconds(30);

        private readonly ILogger<ValidateFhirServerConnectionService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public ValidateFhirServerConnectionService(ILogger<ValidateFhirServerConnectionService> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public async Task<FhirServerConnectionResult> ValidateConnection(ValidateFhirServerConnectionRequest request, CancellationToken cancellationToken)
        {
            using var activity = ServiceActivitySource.Instance.StartActivity("ValidateFhirServerConnectionService.ValidateConnection");

            if (request == null || string.IsNullOrWhiteSpace(request.FhirServerUrl))
            {
                throw new BadRequestException("No FHIR server URL was provided. One is required to validate.");
            }

            var metadataUrl = $"{request.FhirServerUrl.Trim().TrimEnd('/')}/metadata";

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(MetadataTimeout);

            bool isSuccessStatusCode;
            int statusCode;
            string? reasonPhrase;
            string content;
            try
            {
                var httpClient = _httpClientFactory.CreateClient("FhirHttpClient");

                using var metadataRequest = new HttpRequestMessage(HttpMethod.Get, metadataUrl);
                metadataRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/fhir+json"));
                metadataRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var response = await httpClient.SendAsync(metadataRequest, timeoutSource.Token);
                isSuccessStatusCode = response.IsSuccessStatusCode;
                statusCode = (int)response.StatusCode;
                reasonPhrase = response.ReasonPhrase;
                content = await response.Content.ReadAsStringAsync(timeoutSource.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The caller went away - let that surface as a cancellation, not a connection failure.
                throw;
            }
            catch (OperationCanceledException ex)
            {
                // Our own timeout fired: the server never answered.
                _logger.LogError(ex, "Timed out connecting to FHIR server {FhirServerUrl}", metadataUrl.SanitizeForLog());
                throw new FhirConnectionFailedException($"Timed out connecting to the FHIR server at {metadataUrl} after {MetadataTimeout.TotalSeconds} seconds.", ex);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Unable to connect to FHIR server {FhirServerUrl}", metadataUrl.SanitizeForLog());
                throw new FhirConnectionFailedException($"Unable to connect to the FHIR server at {metadataUrl}.", ex);
            }

            if (!isSuccessStatusCode)
            {
                _logger.LogWarning(
                    "FHIR server {FhirServerUrl} returned {StatusCode} for its metadata endpoint",
                    metadataUrl.SanitizeForLog(), statusCode.SanitizeForLog());

                return new FhirServerConnectionResult(false,
                    $"The FHIR server at {metadataUrl} responded with HTTP {statusCode} ({reasonPhrase}). A 200 response containing a CapabilityStatement was expected.");
            }

            try
            {
                // Parse to the base Resource type and then type-check, rather than parsing straight
                // to CapabilityStatement: the permissive parser is lenient about the resource it is
                // handed, so asking for the base type is what actually tells us the server returned
                // a CapabilityStatement and not some other resource.
                var resource = LinkFhirSerializerOptions.FhirJsonParserPermissive.Parse<Resource>(content);

                if (resource is not CapabilityStatement)
                {
                    _logger.LogWarning(
                        "FHIR server {FhirServerUrl} returned a {ResourceType} rather than a CapabilityStatement",
                        metadataUrl.SanitizeForLog(), (resource?.TypeName ?? "null").SanitizeForLog());

                    return new FhirServerConnectionResult(false,
                        $"The FHIR server at {metadataUrl} returned a {resource?.TypeName ?? "null"} resource rather than the expected CapabilityStatement.");
                }
            }
            catch (Exception ex) when (ex is FormatException || ex is StructuralTypeException)
            {
                _logger.LogWarning(ex, "FHIR server {FhirServerUrl} did not return a parsable CapabilityStatement", metadataUrl.SanitizeForLog());

                return new FhirServerConnectionResult(false,
                    $"The FHIR server at {metadataUrl} responded, but the response could not be read as a CapabilityStatement resource: {ex.Message}");
            }

            return new FhirServerConnectionResult(true);
        }
    }
}
