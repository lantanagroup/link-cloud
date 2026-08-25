using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Report.Listeners
{
    public class GenerateReportListener : BackgroundService
    {
        private readonly ILogger<GenerateReportListener> _logger;
        private readonly IKafkaConsumerFactory<string, GenerateReportValue> _kafkaConsumerFactory;
        private readonly ITransientExceptionHandler<GenerateReportListener, string, GenerateReportValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<GenerateReportListener, string, GenerateReportValue> _deadLetterExceptionHandler;
        private readonly ServiceRegistry _serviceRegistry;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<LinkTokenServiceSettings> _linkTokenServiceConfig;
        private readonly IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> _linkBearerServiceOptions;
        private readonly ICreateSystemToken _createSystemToken;

        private readonly DataAcquisitionRequestedProducer _dataAcqProducer;
        private readonly IProducer<string, EvaluationRequestedValue> _evaluationProducer;
        private readonly BlobStorageService _blobStorageService;
        private readonly ServiceInformation _serviceInformation;

        private readonly IExceptionLogger<GenerateReportListener> _exceptionLogger;

        private string Name => this.GetType().Name;

        public GenerateReportListener(ILogger<GenerateReportListener> logger,
            IKafkaConsumerFactory<string, GenerateReportValue> kafkaConsumerFactory,
            ITransientExceptionHandler<GenerateReportListener, string, GenerateReportValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<GenerateReportListener, string, GenerateReportValue> deadLetterExceptionHandler,
            IServiceScopeFactory serviceScopeFactory,
            IHttpClientFactory httpClientFactory,
            IOptions<LinkTokenServiceSettings> linkTokenService,
            ICreateSystemToken createSystemToken,
            IOptions<ServiceRegistry> serviceRegistry,
            DataAcquisitionRequestedProducer dataAcqProducer,
            IProducer<string, EvaluationRequestedValue> evaluationProducer,
            BlobStorageService blobStorageService,
            ServiceInformation serviceInformation,
            IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> linkBearerServiceOptions,
            IExceptionLogger<GenerateReportListener> exceptionLogger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));

            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentException(nameof(transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(deadLetterExceptionHandler));

            _transientExceptionHandler.Topic = nameof(KafkaTopic.GenerateReportRequested) + "-Retry";
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.GenerateReportRequested) + "-Error";
            _httpClientFactory = httpClientFactory;
            _linkTokenServiceConfig = linkTokenService;
            _createSystemToken = createSystemToken;
            _serviceRegistry = serviceRegistry.Value;
            _dataAcqProducer = dataAcqProducer;
            _evaluationProducer = evaluationProducer;
            _blobStorageService = blobStorageService;
            _serviceInformation = serviceInformation;
            _linkBearerServiceOptions = linkBearerServiceOptions;
            _exceptionLogger = exceptionLogger ?? throw new ArgumentNullException(nameof(exceptionLogger));
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async Task StartConsumerLoop(CancellationToken cancellationToken)
        {
            var config = new ConsumerConfig()
            {
                GroupId = _serviceInformation.ServiceConfigName,
                EnableAutoCommit = false,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                SessionTimeoutMs = 10000,
                MaxPollIntervalMs = 300000
            };

            using var consumer = _kafkaConsumerFactory.CreateConsumer(config);
            try
            {
                consumer.Subscribe(nameof(KafkaTopic.GenerateReportRequested));
                _logger.LogInformation("{Name}: Started consumer for topic '{Topic}' at {Timestamp}", Name, nameof(KafkaTopic.GenerateReportRequested), DateTime.UtcNow);

                while (!cancellationToken.IsCancellationRequested)
                {
                    string facilityId = string.Empty;
                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                        {
                            await ProcessMessageAsync(result, consumeCancellationToken);
                            consumer.SafeCommit(result, _logger);
                        }, cancellationToken);

                    }
                    catch (ConsumeException ex)
                    {
                        _exceptionLogger.Handle(ex, "Error consuming message for topics", LogLevel.Error, facilityId, new { Topics = string.Join(", ", consumer.Subscription) });

                        if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                        {
                            throw new OperationCanceledException(ex.Error.Reason, ex);
                        }

                        facilityId = GetFacilityIdFromHeader(ex.ConsumerRecord.Message.Headers);

                        _deadLetterExceptionHandler.HandleConsumeException(ex, facilityId);

                        var offset = ex.ConsumerRecord?.TopicPartitionOffset;
                        consumer.SafeCommit(offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset }, _logger);
                    }
                    catch (Exception ex)
                    {
                        _exceptionLogger.Handle(ex, "Error encountered in GenerateReportListener", LogLevel.Error);
                    }
                }
            }
            catch (OperationCanceledException oce)
            {
                _exceptionLogger.Handle(oce, "Operation Canceled", LogLevel.Error);
                consumer.Close();
                consumer.Dispose();
            }
        }

        public async Task ProcessMessageAsync(ConsumeResult<string, GenerateReportValue> result, CancellationToken cancellationToken)
        {
            string facilityId = string.Empty;
            try
            {
                if (result == null)
                {
                    return;
                }

                using var scope = _serviceScopeFactory.CreateScope();
                var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
                var reportEntryManager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();
                var reportPopulationManager = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();

                var key = result.Message.Key;
                var value = result.Message.Value;
                var inboundMetricsMode = KafkaHeaderHelper.GetMetricsMode(result.Message.Headers);
                var startDate = value.StartDate;
                var endDate = value.EndDate;
                var reportTypes = value.ReportTypes;
                var reportId = value.ReportId;

                facilityId = key;

                if (string.IsNullOrWhiteSpace(facilityId))
                {
                    throw new DeadLetterException("FacilityId is null or empty.");
                }

                if (value is { Regenerate: true, ReportId: not null })
                {
                    var existing = await reportScheduledManager.SingleOrDefaultAsync(x => x.Id == reportId, cancellationToken);

                    if (existing == null)
                    {
                        throw new DeadLetterException("No ReportSchedule found for the provided ID: " + value.ReportId.ToString());
                    }

                    startDate = existing.ReportStartDate.DateTime;
                    endDate = existing.ReportEndDate.DateTime;
                    reportTypes = existing.ReportTypes;
                }
                else
                {
                    if (reportTypes == null || reportTypes.Count == 0)
                    {
                        throw new DeadLetterException("ReportTypes is null or empty.");
                    }

                    if (startDate == null || endDate == null)
                    {
                        throw new DeadLetterException("Start and End dates must be provided.");
                    }
                    if (endDate <= startDate)
                    {
                        throw new DeadLetterException("End date must be after start date.");
                    }
                }

                startDate = new DateTime(startDate.Value.Year, startDate.Value.Month, startDate.Value.Day, startDate.Value.Hour, startDate.Value.Minute, startDate.Value.Second, DateTimeKind.Utc);
                endDate = new DateTime(endDate.Value.Year, endDate.Value.Month, endDate.Value.Day, endDate.Value.Hour, endDate.Value.Minute, endDate.Value.Second, DateTimeKind.Utc);

                bool isCensus = !value.Regenerate && (value.PatientIds == null || value.PatientIds.Count == 0);

                var reportSchedule = new ReportScheduleModel
                {
                    Id = value.AdhocReportId,
                    FacilityId = facilityId,
                    ReportStartDate = startDate.Value,
                    ReportEndDate = endDate.Value,
                    Frequency = Frequency.Adhoc,
                    AdHocType = isCensus ? AdHocType.Census : AdHocType.Manual,
                    ReportTypes = reportTypes,
                    EndOfReportPeriodJobHasRun = true,
                    EnableSubmission = !value.BypassSubmission,
                    CreateDate = DateTime.UtcNow
                };

                var reportName = _blobStorageService.GetReportName(reportSchedule);
                reportSchedule.PayloadRootUri = _blobStorageService.GetUri(reportName)?.ToString();

                await reportScheduledManager.AddAsync(reportSchedule, cancellationToken);

                var newEntries = new List<ReportEntryModel>();

                if (value.Regenerate)
                {
                    var scheduledReports = await reportEntryManager.FindAsync(p => p.ReportScheduleId == reportId, cancellationToken);
                    var patientEntries = scheduledReports.Select(p => p.PatientId).Distinct();

                    foreach (var p in patientEntries)
                    {
                        var newEntry = new ReportEntryModel()
                        {
                            PatientId = p,
                            ReportingStatus = ReportingStatus.PatientIdentified,
                            ReportScheduleId = reportSchedule.Id,
                            FacilityId = facilityId,
                            CreateDate = DateTime.UtcNow,
                            MeasureReports = new List<EntryMeasureReportModel>()
                        };

                        foreach (var reportType in reportTypes)
                        {
                            newEntry.MeasureReports.Add(new EntryMeasureReportModel()
                            {
                                Status = MeasureReportStatus.EntryCreated,
                                ReportType = reportType
                            });
                        }

                        newEntries.Add(newEntry);
                    }
                }
                else
                {
                    _logger.LogInformation("{Name}: Generating new Adhoc report. ReportId: {ReportId}",
                        Name, reportSchedule.Id);

                    if (value.PatientIds == null || value.PatientIds.Count == 0)
                    {
                        _logger.LogDebug("{Name}: Getting Patient List from Census Service. ReportId: {ReportId}",
                            Name, reportSchedule.Id);

                        value.PatientIds =
                            await GetPatientList(facilityId, startDate.Value, endDate.Value, cancellationToken);
                    }

                    var patientIds = value.PatientIds.Distinct().ToList();

                    foreach (var patient in patientIds)
                    {
                        var newEntry = new ReportEntryModel()
                        {
                            PatientId = patient,
                            ReportingStatus = ReportingStatus.PatientIdentified,
                            ReportScheduleId = reportSchedule.Id,
                            FacilityId = facilityId,
                            CreateDate = DateTime.UtcNow,
                            MeasureReports = new List<EntryMeasureReportModel>()
                        };

                        foreach (var reportType in reportTypes)
                        {
                            newEntry.MeasureReports.Add(new EntryMeasureReportModel()
                            {
                                Status = MeasureReportStatus.EntryCreated,
                                ReportType = reportType
                            });
                        }

                        newEntries.Add(newEntry);
                    }
                }

                if (newEntries.Count > 0)
                {
                    await reportEntryManager.AddRangeAsync(newEntries, cancellationToken);
                }

                if (value.Regenerate)
                {
                    // Fire-and-forget Produce + delivery handler preserves per-message error
                    // visibility without serializing a broker round-trip per patient (which
                    // is what awaited ProduceAsync did and what pushed 5000+ patient batches
                    // toward the Kafka consumer poll timeout).
                    var deliveryFailures = new System.Collections.Concurrent.ConcurrentBag<(string PatientId, Error Error)>();

                    foreach (var entry in newEntries)
                    {
                        var capturedPatientId = entry.PatientId;
                        try
                        {
                            _evaluationProducer.Produce(nameof(KafkaTopic.EvaluationRequested),
                                new Message<string, EvaluationRequestedValue>
                                {
                                    Key = facilityId,
                                    Value = new EvaluationRequestedValue
                                    {
                                        PreviousReportId = value.ReportId?.ToString(),
                                        PatientId = entry.PatientId,
                                        ReportTrackingId = reportSchedule.Id.ToString(),
                                    },
                                    Headers = CreateEvaluationRequestedHeaders(inboundMetricsMode)
                                },
                                deliveryReport =>
                                {
                                    if (deliveryReport.Error.IsError)
                                    {
                                        deliveryFailures.Add((capturedPatientId, deliveryReport.Error));
                                    }
                                });
                        }
                        catch (ProduceException<string, EvaluationRequestedValue> ex)
                        {
                            _exceptionLogger.Handle(ex, "An error was encountered generating an Evaluation Requested event", LogLevel.Error, facilityId, new { ReportTrackingId = reportSchedule.Id, entry.PatientId });
                        }
                    }

                    // Flush uses the caller's CancellationToken so we never block longer
                    // than the surrounding consume pipeline allows (shutdown / poll timeout).
                    _evaluationProducer.Flush(cancellationToken);

                    foreach (var failure in deliveryFailures)
                    {
                        _exceptionLogger.Handle(
                            new ProduceException<string, EvaluationRequestedValue>(
                                failure.Error,
                                new DeliveryResult<string, EvaluationRequestedValue> { Topic = nameof(KafkaTopic.EvaluationRequested) }),
                            "Asynchronous delivery of Evaluation Requested event failed",
                            LogLevel.Error,
                            facilityId,
                            new { ReportTrackingId = reportSchedule.Id, PatientId = failure.PatientId });
                    }
                }
                else
                {
                    await _dataAcqProducer.Produce(reportSchedule, newEntries.Select(e => e.PatientId).ToList(), cancellationToken, inboundMetricsMode);
                }
            }
            catch (DeadLetterException ex)
            {
                _deadLetterExceptionHandler.HandleException(result, ex, facilityId);
            }
            catch (TransientException ex)
            {
                _transientExceptionHandler.HandleException(result, ex, facilityId);
            }
            catch (TimeoutException ex)
            {
                var exceptionMessage = $"Timeout exception encountered on {DateTime.UtcNow} for topics: [GenerateReportRequested] at offset: {result.TopicPartitionOffset}";
                var transientException = new TransientException(exceptionMessage, ex);
                _transientExceptionHandler.HandleException(result, transientException, facilityId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _transientExceptionHandler.HandleException(result, ex, facilityId);
            }
        }

        private async Task<List<string>> GetPatientList(string facilityId, DateTime startDate, DateTime enddate, CancellationToken cancellationToken = default)
        {
            string dtFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
            var httpClient = _httpClientFactory.CreateClient();

            string censusRequestUrl = $"{_serviceRegistry.CensusServiceApiUrl}/Census/{Uri.EscapeDataString(facilityId)}/history/admitted?startDate={Uri.EscapeDataString(startDate.ToString(dtFormat))}&endDate={Uri.EscapeDataString(enddate.ToString(dtFormat))}";

            if (_linkTokenServiceConfig.Value.SigningKey is null)
                throw new Exception("Link Token Service Signing Key is missing.");

            if (!_linkBearerServiceOptions.Value.AllowAnonymous)
            {
                var token = await _createSystemToken.ExecuteAsync(_linkTokenServiceConfig.Value.SigningKey, 5);
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // Link the caller's token with the 120s per-request timeout so either signal
            // (consumer shutdown / poll timeout OR request timeout) aborts the HTTP call.
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var censusResponse = await httpClient.GetAsync(censusRequestUrl, linkedCts.Token);
            var censusContent = await censusResponse.Content.ReadAsStringAsync(linkedCts.Token);

            if (!censusResponse.IsSuccessStatusCode)
                throw new TransientException("Response from Census service is not successful: " + censusContent);

            List? admittedPatients;
            admittedPatients = JsonSerializer.Deserialize<List>(censusContent, LinkFhirSerializerOptions.ForFhirLenientSerialization);

            return admittedPatients?.Entry?.Select(p => p.Item.Reference.Split('/').Last()).Distinct().ToList() ?? new List<string>();
        }

        private static Headers CreateEvaluationRequestedHeaders(string? metricsMode)
        {
            var headers = new Headers
            {
                { KafkaConstants.HeaderConstants.CorrelationId, Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) }
            };
            if (!string.IsNullOrWhiteSpace(metricsMode))
            {
                KafkaHeaderHelper.SetMetricsMode(headers, metricsMode);
            }

            return headers;
        }

        private static string GetFacilityIdFromHeader(Headers headers)
        {
            string facilityId = string.Empty;

            if (headers.TryGetLastBytes(KafkaConstants.HeaderConstants.ExceptionFacilityId, out var facilityIdBytes))
            {
                facilityId = Encoding.UTF8.GetString(facilityIdBytes);
            }

            return facilityId;
        }
    }
}