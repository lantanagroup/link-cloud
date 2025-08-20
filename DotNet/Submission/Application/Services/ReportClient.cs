using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace LantanaGroup.Link.Submission.Application.Services
{
    public class ReportClient
    {
        private static readonly JsonSerializerOptions lenientJsonOptions =
            new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector).UsingMode(DeserializerModes.Ostrich);

        private readonly ILogger<ReportClient> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<ServiceRegistry> _serviceRegistry;
        private readonly IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> _linkBearerServiceOptions;
        private readonly IOptions<LinkTokenServiceSettings> _tokenServiceSettings;
        private readonly IServiceScopeFactory _scopeFactory;

        public ReportClient(
            ILogger<ReportClient> logger,
            IHttpClientFactory httpClientFactory,
            IOptions<ServiceRegistry> serviceRegistry,
            IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> linkBearerServiceOptions,
            IOptions<LinkTokenServiceSettings> tokenServiceSettings,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
            if (string.IsNullOrEmpty(serviceRegistry.Value?.ReportServiceApiUrl))
            {
                throw new Exception("Report Service API Url is missing from Service Registry.");
            }
            _linkBearerServiceOptions = linkBearerServiceOptions ?? throw new ArgumentNullException(nameof(linkBearerServiceOptions));
            _tokenServiceSettings = tokenServiceSettings ?? throw new ArgumentNullException(nameof(tokenServiceSettings));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        }

        public async Task<Bundle?> GetSubmissionBundleForPatientAsync(string facilityId, string patientId, string reportScheduleId)
        {
            _logger.LogDebug("Retrieving submission bundle from Report");
            var client = await GetClientAsync();
            var response = await client.GetStringAsync(
                $"{_serviceRegistry.Value.ReportServiceApiUrl}/Report/Bundle/Patient" +
                $"?facilityId={facilityId.SanitizeAndRemove()}" +
                $"&patientId={patientId.SanitizeAndRemove}" +
                $"&reportScheduleId={reportScheduleId.SanitizeAndRemove()}");
            var model = JsonSerializer.Deserialize<PatientSubmissionModel>(response, lenientJsonOptions);
            return model?.Bundle;
        }

        public async Task<Bundle?> GetManifestBundleAsync(string facilityId, string reportScheduleId)
        {
            _logger.LogDebug("Retrieving manifest bundle from Report");
            var client = await GetClientAsync();
            var response = await client.GetStringAsync(
                $"{_serviceRegistry.Value.ReportServiceApiUrl}/Report/Bundle/Manifest" +
                $"?facilityId={facilityId.SanitizeAndRemove()}" +
                $"&reportScheduleId={reportScheduleId.SanitizeAndRemove()}");
            return JsonSerializer.Deserialize<Bundle>(response, lenientJsonOptions);
        }

        private async Task<HttpClient> GetClientAsync()
        {
            HttpClient client = _httpClientFactory.CreateClient();

            if (!_linkBearerServiceOptions.Value.AllowAnonymous)
            {
                if (_tokenServiceSettings.Value.SigningKey is null)
                    throw new Exception("Link Token Service Signing Key is missing.");

                //Add link token
                var createSystemToken = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ICreateSystemToken>();
                var token = await createSystemToken.ExecuteAsync(_tokenServiceSettings.Value.SigningKey, 5);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return client;
        }
    }
}
