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
        }

        public async Task<HttpResponseMessage> ServiceHealthCheck(CancellationToken cancellationToken)
        {
            //check if the submission service uri is set
            if (string.IsNullOrEmpty(_serviceRegistry.Value.SubmissionServiceUrl))
            {
                _logger.LogGatewayServiceUriException("Submission", "Submission service uri is not set");
                return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
            }

            _client.BaseAddress = new Uri(_serviceRegistry.Value.SubmissionServiceUrl);
            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // HTTP GET
            HttpResponseMessage response = await _client.GetAsync($"{_serviceRegistry.Value.SubmissionServiceUrl}/health", cancellationToken);

            return response;
        }
    }
}
