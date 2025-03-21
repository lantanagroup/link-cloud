
using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services.Security;
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
        private readonly ITransientExceptionHandler<string, GenerateReportValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<string, GenerateReportValue> _deadLetterExceptionHandler;
        private readonly ServiceRegistry _serviceRegistry;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<LinkTokenServiceSettings> _linkTokenServiceConfig;
        private readonly ICreateSystemToken _createSystemToken;

        private readonly IProducer<string, EvaluationRequestedValue> _evaluationProducer;

        private readonly DataAcquisitionRequestedProducer _dataAcqProducer;

        private string Name => this.GetType().Name;

        public GenerateReportListener(ILogger<GenerateReportListener> logger,
            IKafkaConsumerFactory<string, GenerateReportValue> kafkaConsumerFactory,
            ITransientExceptionHandler<string, GenerateReportValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<string, GenerateReportValue> deadLetterExceptionHandler,
            IServiceScopeFactory serviceScopeFactory,
            IHttpClientFactory httpClientFactory,
            IOptions<LinkTokenServiceSettings> linkTokenService,
            ICreateSystemToken createSystemToken,
            IOptions<ServiceRegistry> serviceRegistry,
            DataAcquisitionRequestedProducer dataAcqProducer,
            IProducer<string, EvaluationRequestedValue> evaluationProducer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));

            _transientExceptionHandler = transientExceptionHandler ??
                                               throw new ArgumentException(nameof(_deadLetterExceptionHandler));

            _deadLetterExceptionHandler = deadLetterExceptionHandler ??
                                               throw new ArgumentException(nameof(_deadLetterExceptionHandler));

            _transientExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _transientExceptionHandler.Topic = nameof(KafkaTopic.GenerateReportRequested) + "-Retry";

            _deadLetterExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.GenerateReportRequested) + "-Error";
            _httpClientFactory = httpClientFactory;
            _linkTokenServiceConfig = linkTokenService;
            _createSystemToken = createSystemToken;
            _serviceRegistry = serviceRegistry.Value;
            _dataAcqProducer = dataAcqProducer;
            _evaluationProducer = evaluationProducer;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }


        private async Task StartConsumerLoop(CancellationToken cancellationToken)
        {
            var config = new ConsumerConfig()
            {
                GroupId = ReportConstants.ServiceName,
                EnableAutoCommit = false,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                SessionTimeoutMs = 10000,
                MaxPollIntervalMs = 300000
            };

            using var consumer = _kafkaConsumerFactory.CreateConsumer(config);
            try
            {
                consumer.Subscribe(nameof(KafkaTopic.GenerateReportRequested));
                _logger.LogInformation($"Started Genearate Report consumer for topic '{nameof(KafkaTopic.GenerateReportRequested)}' at {DateTime.UtcNow}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    string facilityId = string.Empty;
                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                        {
                            if (result == null)
                            {
                                consumer.Commit();
                                return;
                            }

                            try
                            {
                                using var scope = _serviceScopeFactory.CreateScope();
                                var measureReportScheduledManager =
                                    scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

                                var key = result.Message.Key;
                                var value = result.Message.Value;
                                var startDate = value.StartDate;
                                var endDate = value.EndDate;
                                var reportTypes = value.ReportTypes;

                                facilityId = key;

                                if (string.IsNullOrWhiteSpace(facilityId))
                                {
                                    throw new DeadLetterException(
                                        $"{Name}: FacilityId is null or empty.");
                                }

                                //If we are re-running an existing report, fetch the details from the database and replace the Values retrieved from the message
                                if (value.ReportId != null)
                                {
                                    var existing = await measureReportScheduledManager.SingleOrDefaultAsync(x => x.Id == value.ReportId, consumeCancellationToken);

                                    if (existing == null)
                                    {
                                        throw new DeadLetterException("No ReportSchedule found for the provided ID: " + HtmlInputSanitizer.Sanitize(value.ReportId));
                                    }

                                    startDate = existing.ReportStartDate;
                                    endDate = existing.ReportEndDate;
                                    reportTypes = existing.ReportTypes;
                                }
                                else //Otherwise validate the values from the message
                                {
                                    if (reportTypes == null || reportTypes.Count == 0)
                                    {
                                        throw new DeadLetterException(
                                            $"{Name}: ReportTypes is null or empty.");
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

                                // Create ReportSchedule for AdHoc Report
                                var reportSchedule = new ReportScheduleModel
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    FacilityId = facilityId,
                                    ReportStartDate = startDate.Value,
                                    ReportEndDate = endDate.Value,
                                    Frequency = Frequency.Adhoc,
                                    ReportTypes = reportTypes,
                                    EndOfReportPeriodJobHasRun = true,
                                    EnableSubmission = !value.BypassSubmission,
                                    CreateDate = DateTime.UtcNow
                                };

                                await measureReportScheduledManager.AddAsync(reportSchedule, cancellationToken);

                                var submissionEntryManager = scope.ServiceProvider.GetRequiredService<ISubmissionEntryManager>();

                                if (value.ReportId != null)
                                {
                                    var pids = (await submissionEntryManager.FindAsync(p => p.ReportScheduleId == value.ReportId, cancellationToken)).Select(p => p.PatientId);

                                    pids.AsParallel().ForAll(async p =>
                                    {
                                        await _evaluationProducer.ProduceAsync(nameof(KafkaTopic.EvaluationRequested), new Message<string, EvaluationRequestedValue>
                                        {
                                            Key = facilityId,
                                            Value = new EvaluationRequestedValue
                                            {
                                                PreviousReportId = value.ReportId,
                                                PatientId = p,
                                                ReportTrackingId = reportSchedule.Id
                                            },
                                            Headers = new Headers
                                            {
                                                { "X-Correlation-Id", Encoding.ASCII.GetBytes(Guid.NewGuid().ToString()) }
                                            }
                                        });
                                    });
                                }
                                else
                                {
                                    // Get Patient List if none was provided
                                    if (value.PatientIds == null || value.PatientIds.Count == 0)
                                    {
                                        value.PatientIds = await GetPatientList(facilityId, startDate.Value, endDate.Value);
                                    }

                                    value.PatientIds.AsParallel().ForAll(async patient =>
                                    {
                                        //For each patient and report type, Create Submission Entries for each Patient and Report Type
                                        foreach (var reportType in reportTypes)
                                        {
                                            await submissionEntryManager.AddAsync(new MeasureReportSubmissionEntryModel()
                                            {
                                                PatientId = patient,
                                                Status = PatientSubmissionStatus.PendingEvaluation,
                                                ReportScheduleId = reportSchedule.Id,
                                                FacilityId = facilityId,
                                                ReportType = reportType,
                                                CreateDate = DateTime.UtcNow
                                            }, cancellationToken);
                                        }
                                    });

                                    //Submit a Data Acquisition Request for each patient
                                    await _dataAcqProducer.Produce(reportSchedule, value.PatientIds);
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
                                var transientException = new TransientException(ex.Message, ex.InnerException);

                                _transientExceptionHandler.HandleException(result, transientException, facilityId);
                            }
                            catch (Exception ex)
                            {
                                _transientExceptionHandler.HandleException(result, ex, facilityId);
                            }
                            finally
                            {
                                consumer.Commit(result);
                            }
                        }, cancellationToken);

                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Error consuming message for topics: [{1}] at {2}", string.Join(", ", consumer.Subscription), DateTime.UtcNow);

                        if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                        {
                            throw new OperationCanceledException(ex.Error.Reason, ex);
                        }

                        facilityId = GetFacilityIdFromHeader(ex.ConsumerRecord.Message.Headers);

                        _deadLetterExceptionHandler.HandleConsumeException(ex, facilityId);

                        var offset = ex.ConsumerRecord?.TopicPartitionOffset;
                        consumer.Commit(offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error encountered in GenerateReportListener");
                        consumer.Commit();
                    }
                }
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogError(oce, $"Operation Canceled: {oce.Message}");
                consumer.Close();
                consumer.Dispose();
            }
        }

        private async Task<List<string>> GetPatientList(string facilityId, DateTime startDate, DateTime enddate)
        {
            string dtFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
            var httpClient = _httpClientFactory.CreateClient();

            string censusRequestUrl = $"{_serviceRegistry.CensusServiceApiUrl}/Census/{Uri.EscapeDataString(facilityId)}/history/admitted?startDate={Uri.EscapeDataString(startDate.ToString(dtFormat))}&endDate={Uri.EscapeDataString(enddate.ToString(dtFormat))}";

            if (_linkTokenServiceConfig.Value.SigningKey is null)
                throw new Exception("Link Token Service Signing Key is missing.");

            //Add link token
            var token = await _createSystemToken.ExecuteAsync(_linkTokenServiceConfig.Value.SigningKey, 5);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var censusResponse = await httpClient.GetAsync(censusRequestUrl, cts.Token);
            var censusContent = await censusResponse.Content.ReadAsStringAsync(cts.Token);

            if (!censusResponse.IsSuccessStatusCode)
                throw new TransientException("Response from Census service is not successful: " + censusContent);

            List? admittedPatients;
            try
            {
                admittedPatients =
                    System.Text.Json.JsonSerializer.Deserialize<List>(
                        censusContent,
                        new JsonSerializerOptions().ForFhir());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deserializing admitted patients from Census service response.");
                _logger.LogDebug("Census service response: " + censusContent);
                throw new TransientException("Error deserializing admitted patients from Census service response: " + ex.Message + Environment.NewLine + ex.StackTrace, ex.InnerException);
            }

            return admittedPatients?.Entry?.Select(p => p.Item.Reference.Split('/').Last()).Distinct().ToList() ?? new List<string>();
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
