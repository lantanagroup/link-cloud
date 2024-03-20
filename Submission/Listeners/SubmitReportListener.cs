using System.Transactions;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
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
        private readonly IHttpClientFactory _httpClient;

        private readonly ITransientExceptionHandler<SubmitReportKey, SubmitReportValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<SubmitReportKey, SubmitReportValue> _deadLetterExceptionHandler;

        public SubmitReportListener(ILogger<SubmitReportListener> logger, IKafkaConsumerFactory<SubmitReportKey, SubmitReportValue> kafkaConsumerFactory,
            IMediator mediator, IOptions<SubmissionServiceConfig> submissionConfig, IOptions<FileSystemConfig> fileSystemConfig, IHttpClientFactory httpClient,
            ITransientExceptionHandler<SubmitReportKey, SubmitReportValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<SubmitReportKey, SubmitReportValue> deadLetterExceptionHandler)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _mediator = mediator ?? throw new ArgumentException(nameof(mediator));
            _submissionConfig = submissionConfig.Value;
            _fileSystemConfig = fileSystemConfig.Value;
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(HttpClient));

            _transientExceptionHandler = transientExceptionHandler ??
                                         throw new ArgumentException(nameof(transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ??
                                          throw new ArgumentException(nameof(deadLetterExceptionHandler));

            var t = (TransientExceptionHandler<SubmitReportKey, SubmitReportValue>)_transientExceptionHandler;
            t.ServiceName = "Submission";
            t.Topic = nameof(KafkaTopic.SubmitReport) + "-Retry";

            var d = (DeadLetterExceptionHandler<SubmitReportKey, SubmitReportValue>)_deadLetterExceptionHandler;
            d.ServiceName = "Submission";
            d.Topic = nameof(KafkaTopic.SubmitReport) + "-Error";
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

            using var consumer = _kafkaConsumerFactory.CreateConsumer(config);
            try
            {
                consumer.Subscribe(nameof(KafkaTopic.SubmitReport));
                _logger.LogInformation($"Started consumer for topic '{nameof(KafkaTopic.SubmitReport)}' at {DateTime.UtcNow}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    var consumeResult = new ConsumeResult<SubmitReportKey, SubmitReportValue>();
                    try
                    {
                        consumeResult = consumer.Consume(cancellationToken);
                        if (consumeResult == null)
                        {
                            consumeResult = new ConsumeResult<SubmitReportKey, SubmitReportValue>();
                            throw new DeadLetterException(
                                "SubmitReportListener: Result of ConsumeResult<ReportSubmittedKey, ReportSubmittedValue>.Consume is null");
                        }

                        var key = consumeResult.Message.Key;
                        var value = consumeResult.Message.Value;

                        if (string.IsNullOrWhiteSpace(key.FacilityId) ||
                            string.IsNullOrWhiteSpace(key.ReportType) ||
                            string.IsNullOrWhiteSpace(value.MeasureReportScheduleId))
                        {
                            throw new DeadLetterException(
                                "SubmitReportListener: One or more required MeasureReportScheduledKey properties are null or empty.");
                        }

                        string requestUrl = _submissionConfig.ReportServiceUrl + $"?reportId={value.MeasureReportScheduleId}";

                        var response = await _httpClient.CreateClient().GetAsync(requestUrl, cancellationToken);
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
                            throw new TransientException("SubmitReportListener: Bundle File Not Created");
                        }
                        #endregion

                        consumer.Commit(consumeResult);
                    }
                    catch (ConsumeException ex)
                    {
                        consumer.Commit(consumeResult);
                        _deadLetterExceptionHandler.HandleException(consumeResult, new DeadLetterException("ReportScheduledListener: " + ex.Message, ex.InnerException));
                    }
                    catch (DeadLetterException ex)
                    {
                        consumer.Commit(consumeResult);
                        _deadLetterExceptionHandler.HandleException(consumeResult, ex);
                    }
                    catch (TransientException ex)
                    {
                        _transientExceptionHandler.HandleException(consumeResult, ex);
                        consumer.Commit(consumeResult);
                    }
                    catch (Exception ex)
                    {
                        consumer.Commit(consumeResult);
                        _deadLetterExceptionHandler.HandleException(consumeResult, new DeadLetterException("ReportScheduledListener: " + ex.Message, ex.InnerException));
                    }
                }
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogError($"Operation Canceled: {oce.Message}", oce);
                consumer.Close();
                consumer.Dispose();
            }

        }
    }
}
