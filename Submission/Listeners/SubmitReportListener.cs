using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Submission.Application.Models;
using MediatR;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace LantanaGroup.Link.Submission.Listeners
{
    public class SubmitReportListener : BackgroundService
    {
        private readonly ILogger<SubmitReportListener> _logger;
        private readonly IKafkaConsumerFactory<SubmitReportKey, SubmitReportValue> _kafkaConsumerFactory;
        private readonly IMediator _mediator;
        private readonly SubmissionServiceConfig _submissionConfig;
        private readonly FileSystemConfig _fileSystemConfig;
        private readonly HttpClient _httpClient;

        public SubmitReportListener(ILogger<SubmitReportListener> logger, IKafkaConsumerFactory<SubmitReportKey, SubmitReportValue> kafkaConsumerFactory,
            IMediator mediator, IOptions<SubmissionServiceConfig> submissionConfig, IOptions<FileSystemConfig> fileSystemConfig, HttpClient httpClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _mediator = mediator ?? throw new ArgumentException(nameof(mediator));
            _submissionConfig = submissionConfig.Value;
            _fileSystemConfig = fileSystemConfig.Value;
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(HttpClient));
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }


        private async void StartConsumerLoop(CancellationToken cancellationToken)
        {
            var config = new ConsumerConfig()
            {
                GroupId = "SubmitReportEvent",
                EnableAutoCommit = false
            };

            using (var _submitReportConsumer = _kafkaConsumerFactory.CreateConsumer(config))
            {
                try
                {
                    _submitReportConsumer.Subscribe(nameof(KafkaTopic.SubmitReport));
                    _logger.LogInformation($"Started consumer for topic '{nameof(KafkaTopic.SubmitReport)}' at {DateTime.UtcNow}");

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            var consumeResult = _submitReportConsumer.Consume(cancellationToken);

                            try
                            {
                                var key = consumeResult.Message.Key;
                                var value = consumeResult.Message.Value;

                                if (string.IsNullOrWhiteSpace(value.MeasureReportScheduleId))
                                {
                                    throw new InvalidOperationException("MeasureReportScheduleId is null or empty");
                                }

                                string requestUrl = _submissionConfig.ReportServiceUrl + $"?reportId={value.MeasureReportScheduleId}";

                                var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
                                var measureReportSubmissionBundle =
                                    JsonConvert.DeserializeObject<MeasureReportSubmissionModel>(await response.Content.ReadAsStringAsync(cancellationToken));

                                #region File IO
                                string facilityDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _fileSystemConfig.FilePath.Trim('/'), key.FacilityId);
                                if (!Directory.Exists(facilityDirectory))
                                {
                                    Directory.CreateDirectory(facilityDirectory);
                                }

                                var dtu = DateTime.UtcNow;
                                string fullFilePath = facilityDirectory + $"/submission_{value.MeasureReportScheduleId.Replace("-", "_")}.txt";

                                await File.WriteAllTextAsync(fullFilePath, measureReportSubmissionBundle.SubmissionBundle, cancellationToken);

                                if (!File.Exists(fullFilePath))
                                {
                                    throw new Exception("SubmitReportListener: Bundle File Not Created");
                                }
                                #endregion

                                _submitReportConsumer.Commit(consumeResult);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Error occurred during SubmitReportListener: {ex.Message}");
                                throw;
                            }
                        }
                        catch (ConsumeException e)
                        {
                            _logger.LogError(e, $"Consumer error: {e.Error.Reason}");
                            if (e.Error.IsFatal)
                            {
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"An exception occurred in the Submit Report Consumer service: {ex.Message}", ex);
                        }
                    }
                }
                catch (OperationCanceledException oce)
                {
                    _logger.LogError($"Operation Canceled: {oce.Message}", oce);
                    _submitReportConsumer.Close();
                    _submitReportConsumer.Dispose();
                }
            }

        }
    }
}
