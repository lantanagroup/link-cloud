using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Clients
{
    public class SubmissionService
    {
        private readonly ILogger<SubmissionService> _logger;
        private readonly HttpClient _client;
        private readonly IOptions<ServiceRegistry> _serviceRegistry;

        public SubmissionService(ILogger<SubmissionService> logger, HttpClient client, IOptions<ServiceRegistry> serviceRegistry)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));

            InitHttpClient();
        }

        public async Task<HttpResponseMessage> ServiceHealthCheck(CancellationToken cancellationToken)
        {
            // HTTP GET
            HttpResponseMessage response = await _client.GetAsync($"health", cancellationToken);

            return response;
        }

        private void InitHttpClient()
        {
            //check if the service uri is set
            if (string.IsNullOrEmpty(_serviceRegistry.Value.SubmissionServiceUrl))
            {
                _logger.LogGatewayServiceUriException("Submission", "Submission service uri is not set");
                throw new ArgumentNullException("Submission Service URL is missing.");
            }

            _client.BaseAddress = new Uri(_serviceRegistry.Value.SubmissionServiceUrl);
            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }
}
