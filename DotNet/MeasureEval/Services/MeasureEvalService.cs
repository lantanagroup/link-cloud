using Flurl;
using Grpc.Core;
using LantanaGroup.Link.MeasureEval.Models;
using System.Text;
using Confluent.Kafka;
using System.Net;
using LantanaGroup.Link.Shared.Application.Wrappers;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.MeasureEval.Services
{
    public class MeasureEvalService 
    {
        private readonly ILogger<MeasureEvalService> _logger;
        private readonly MeasureEvalConfig _measureEvalConfig;
        private readonly HttpClient httpClient;
        private readonly IKafkaWrapper<Ignore, Null, Null, MeasureChanged> kafkaWrapper;

        public MeasureEvalService(ILogger<MeasureEvalService> logger, HttpClient httpClient, IKafkaWrapper<Ignore, Null, Null, MeasureChanged> kafkaWrapper, MeasureEvalConfig measureEvalConfig)
        {
            _logger = logger;
            this._measureEvalConfig = measureEvalConfig ?? throw new ArgumentNullException(nameof(MeasureEvalConfig));
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(HttpClient));
            this.kafkaWrapper = kafkaWrapper ?? throw new ArgumentNullException(nameof(kafkaWrapper));
        }


        public async Task<HttpResponseMessage> UpdateMeasure(string measureId, string measure)
        {
            HttpContent requestBody = GenerateBody(measure, "application/json");
            
            if (_measureEvalConfig.EvaluationServiceUrl != null && requestBody != null)
            {
                try
                {
                    var response = await httpClient.PostAsync(_measureEvalConfig.EvaluationServiceUrl, requestBody);
                    return response;
                }catch(Exception ex)
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
