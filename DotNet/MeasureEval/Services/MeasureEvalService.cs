using Confluent.Kafka;
using LantanaGroup.Link.MeasureEval.Models;
using LantanaGroup.Link.Shared.Application.Interfaces;
using Microsoft.Extensions.Options;
using System.Text;

namespace LantanaGroup.Link.MeasureEval.Services
{
    public class MeasureEvalService
    {
        private readonly ILogger<MeasureEvalService> _logger;
        private readonly IOptions<MeasureEvalConfig> _measureEvalConfig;
        private readonly HttpClient httpClient;

        public MeasureEvalService(ILogger<MeasureEvalService> logger, HttpClient httpClient, IOptions<MeasureEvalConfig> measureEvalConfig)
        {
            _logger = logger;
            this._measureEvalConfig = measureEvalConfig ?? throw new ArgumentNullException(nameof(MeasureEvalConfig));
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(HttpClient));
        }


        public async Task<HttpResponseMessage> UpdateMeasure(string measureId, string measure)
        {
            HttpContent requestBody = GenerateBody(measure, "application/json");

            if (_measureEvalConfig.Value.EvaluationServiceUrl != null && requestBody != null)
            {
                try
                {
                    var response = await httpClient.PostAsync(_measureEvalConfig.Value.EvaluationServiceUrl, requestBody);
                    return response;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error occurred during Update: {ex.Message}");
                    return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
                }

            }
            return null;

        }

        public StringContent GenerateBody(string data, string contentType)
        {
            return new StringContent(data, Encoding.UTF8, contentType);
        }

    }
}
