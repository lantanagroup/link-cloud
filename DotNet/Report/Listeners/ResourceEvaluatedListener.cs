using Confluent.Kafka;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Commands;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Queries;
using LantanaGroup.Link.Report.Application.MeasureReportSubmissionEntry.Commands;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using MediatR;
using System.Text;
using System.Text.Json;
using System.Transactions;
using Task = System.Threading.Tasks.Task;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Report.Application.MeasureReportSubmissionEntry.Queries;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Report.Core;

namespace LantanaGroup.Link.Report.Listeners
{
    public class ResourceEvaluatedListener : BackgroundService
    {

        private readonly ILogger<ResourceEvaluatedListener> _logger;
        private readonly IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue> _kafkaConsumerFactory;
        private readonly IKafkaProducerFactory<SubmissionReportKey, SubmissionReportValue> _kafkaProducerFactory;
        private readonly IMediator _mediator;

        private readonly ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue> _deadLetterExceptionHandler;

        private readonly MeasureReportSubmissionBundler _bundler;
        private readonly MeasureReportAggregator _aggregator;
        
        private string Name => this.GetType().Name;

        public ResourceEvaluatedListener(ILogger<ResourceEvaluatedListener> logger, IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue> kafkaConsumerFactory,
            IKafkaProducerFactory<SubmissionReportKey, SubmissionReportValue> kafkaProducerFactory, IMediator mediator,
            ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue> deadLetterExceptionHandler,
            MeasureReportSubmissionBundler bundler,
            MeasureReportAggregator aggregator)
        {

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentException(nameof(kafkaProducerFactory));
            _mediator = mediator ?? throw new ArgumentException(nameof(mediator));
            _bundler = bundler;
            _aggregator = aggregator;
            
            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentException(nameof(transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(deadLetterExceptionHandler));

            _transientExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _transientExceptionHandler.Topic = nameof(KafkaTopic.ResourceEvaluated) + "-Retry";

            _deadLetterExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.ResourceEvaluated) + "-Error";
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async void StartConsumerLoop(CancellationToken cancellationToken)
        {
            var consumerConfig = new ConsumerConfig()
            {
                GroupId = ReportConstants.ServiceName,
                EnableAutoCommit = false
            };

            ProducerConfig producerConfig = new ProducerConfig()
            {
                ClientId = "Report_SubmissionReportScheduled"
            };

            using var consumer = _kafkaConsumerFactory.CreateConsumer(consumerConfig);
            try
            {
                consumer.Subscribe(nameof(KafkaTopic.ResourceEvaluated));
                _logger.LogInformation($"Started resource evaluated consumer for topic '{nameof(KafkaTopic.ResourceEvaluated)}' at {DateTime.UtcNow}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    ConsumeResult<ResourceEvaluatedKey, ResourceEvaluatedValue>? consumeResult = null;
                    var facilityId = string.Empty;
                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, cancellationToken) =>
                        {
                            consumeResult = result;

                            if (consumeResult == null)
                            {
                                throw new DeadLetterException($"{Name}: consumeResult is null", AuditEventType.Create);
                            }

                            var key = consumeResult.Message.Key;
                            var value = consumeResult.Message.Value;
                            facilityId = key.FacilityId;

                            if (!consumeResult.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
                            {
                                _logger.LogInformation($"{Name}: Received message without correlation ID: {consumeResult.Topic}");
                            }

                            if (string.IsNullOrWhiteSpace(key.FacilityId) ||
                                string.IsNullOrWhiteSpace(key.ReportType) ||
                                key.StartDate == DateTime.MinValue ||
                                key.EndDate == DateTime.MinValue)
                            {
                                throw new DeadLetterException(
                                    $"{Name}: One or more required Key/Value properties are null, empty, or otherwise invalid.", AuditEventType.Create);
                            }

                            // find existing report scheduled for this facility, report type, and date range
                            var schedule = await _mediator.Send(
                                               new FindMeasureReportScheduleForReportTypeQuery
                                               {
                                                   FacilityId = key.FacilityId,
                                                   ReportStartDate = key.StartDate,
                                                   ReportEndDate = key.EndDate,
                                                   ReportType = key.ReportType
                                               }, cancellationToken)
                                           ?? throw new TransactionException(
                                               $"{Name}: report schedule not found for Facility {key.FacilityId} and reporting period of {key.StartDate} - {key.EndDate} for {key.ReportType}");

                            var entry = await _mediator.Send(new GetMeasureReportSubmissionEntryCommand() { MeasureReportScheduleId = schedule.Id, PatientId = value.PatientId });

                            if (entry == null)
                            {
                                entry = new MeasureReportSubmissionEntryModel
                                {
                                    FacilityId = key.FacilityId,
                                    MeasureReportScheduleId = schedule.Id,
                                    PatientId = value.PatientId
                                };
                            }

                            var resource = JsonSerializer.Deserialize<Resource>(value.Resource.ToString(), new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector, new FhirJsonPocoDeserializerSettings { Validator = null }));

                            if (resource == null)
                            {
                                throw new DeadLetterException($"{Name}: Unable to deserialize event resource", AuditEventType.Create);
                            }
                            else if (resource.TypeName == "MeasureReport")
                            {
                                entry.AddMeasureReport((MeasureReport)resource);
                            }
                            else
                            {
                                entry.AddContainedResource(resource);
                            }

                            if (entry.Id == null)
                            {
                                await _mediator.Send(new CreateMeasureReportSubmissionEntryCommand
                                {
                                    MeasureReportSubmissionEntry = entry
                                }, cancellationToken);
                            }
                            else
                            {
                                await _mediator.Send(new UpdateMeasureReportSubmissionEntryCommand
                                {
                                    MeasureReportSubmissionEntry = entry
                                }, cancellationToken);
                            }

                            #region Patients To Query & Submision Report Handling



                            if (schedule.PatientsToQueryDataRequested.GetValueOrDefault())
                            {
                                if (schedule.PatientsToQuery?.Contains(value.PatientId) ?? false)
                                {
                                    schedule.PatientsToQuery.Remove(value.PatientId);

                                    await _mediator.Send(new UpdateMeasureReportScheduleCommand
                                    {
                                        ReportSchedule = schedule
                                    }, cancellationToken);
                                }

                                var submissionEntries = (await _mediator.Send(new GetMeasureReportSubmissionEntriesQuery() { MeasureReportScheduleId = schedule.Id })).ToList();

                                var allReady = submissionEntries.All(x => x.ReadyForSubmission);

                                if ((schedule.PatientsToQuery?.Count ?? 0) == 0 && allReady)
                                {
                                    var patientIds = submissionEntries.Select(s => s.PatientId).ToList();

                                    var parser = new FhirJsonParser();
                                    List<MeasureReport> measureReports = submissionEntries
                                        .Select(e => parser.Parse<MeasureReport>(e.MeasureReport))
                                        .ToList();

                                    using var prod = _kafkaProducerFactory.CreateProducer(producerConfig);

                                    prod.Produce(nameof(KafkaTopic.SubmitReport),
                                        new Message<SubmissionReportKey, SubmissionReportValue>
                                        {
                                            Key = new SubmissionReportKey()
                                            {
                                                FacilityId = schedule.FacilityId,
                                                StartDate = schedule.ReportStartDate,
                                                EndDate = schedule.ReportEndDate
                                            },
                                            Value = new SubmissionReportValue()
                                            {
                                                PatientIds = patientIds,
                                                MeasureIds = string.Join("+", measureReports.Select(mr => mr.Measure).Distinct()),
                                                Organization = _bundler.CreateOrganization(schedule.FacilityId),
                                                Aggregates = _aggregator.Aggregate(measureReports)
                                            },
                                            Headers = new Headers
                                            {
                                            { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) }
                                            }
                                        });

                                    prod.Flush(cancellationToken);
                                }
                            }
                            #endregion

                        }, cancellationToken);
                        
                    }
                    catch (ConsumeException ex)
                    {
                        _deadLetterExceptionHandler.HandleException(new DeadLetterException($"{Name}: " + ex.Message, AuditEventType.Create, ex.InnerException), facilityId);
                    }
                    catch (DeadLetterException ex)
                    {
                        _deadLetterExceptionHandler.HandleException(consumeResult, ex, facilityId);
                    }
                    catch (TransientException ex)
                    {
                        _transientExceptionHandler.HandleException(consumeResult, ex, facilityId);
                    }
                    catch (TimeoutException ex)
                    {
                        var transientException = new TransientException(ex.Message, AuditEventType.Submit, ex.InnerException);

                        _transientExceptionHandler.HandleException(consumeResult, transientException, facilityId);
                    }
                    catch (Exception ex)
                    {
                        _deadLetterExceptionHandler.HandleException(ex,facilityId, AuditEventType.Create);
                    }
                    finally
                    {
                        if (consumeResult != null)
                        {
                            consumer.Commit(consumeResult);
                        }
                        else
                        {
                            consumer.Commit();
                        }
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

    }
}
