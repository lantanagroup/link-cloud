using Confluent.Kafka;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration
{
    public class CreateReportScheduled : ICreateReportScheduled
    {
        private readonly ILogger<CreateReportScheduled> _logger;
        private readonly IProducer<string, object> _producer;
        private const double DEFAULT_DELAY_MINUTES = 5;
        private const double MAX_DELAY_MINUTES = 60 * 24;

        public CreateReportScheduled(ILogger<CreateReportScheduled> logger, IProducer<string, object> producer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        }

        public async Task<string> Execute(ReportScheduled model, string? userId = null)
        {
            using var activity = ServiceActivitySource.Instance.StartActivity("Producing Report Scheduled Event");
            var correlationId = model.reportTrackingId; //Guid.NewGuid().ToString();

            try
            {
                if (string.IsNullOrEmpty(model.FacilityId))
                {
                    throw new ArgumentException("FacilityId cannot be null or empty");
                }
                
                var headers = new Headers
                {
                    { "X-Correlation-Id", System.Text.Encoding.ASCII.GetBytes(correlationId) }
                };
                
                DateTime endDate;

                if (double.TryParse(model.Delay, out double delay))
                {
                    if (delay < 0)
                    {
                        throw new ArgumentException("Delay cannot be negative", nameof(model.Delay));
                    }
                    if (delay > MAX_DELAY_MINUTES)
                    {
                        throw new ArgumentException($"Delay cannot exceed {MAX_DELAY_MINUTES} minutes", nameof(model.Delay));
                    }
                    endDate = DateTime.UtcNow.AddMinutes(delay);
                }
                else
                {
                    _logger.LogWarning("Invalid delay value '{Delay}'. Using default delay of {DefaultDelay} minutes", HtmlInputSanitizer.Sanitize(model.Delay), DEFAULT_DELAY_MINUTES);
                    endDate = DateTime.UtcNow.AddMinutes(DEFAULT_DELAY_MINUTES); // default to 5 minutes
                }
               
                 var normalizedEndDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, endDate.Hour, endDate.Minute, 0, DateTimeKind.Utc);
                 if (model.ReportTypes == null || !model.ReportTypes.Any())
                 {
                    throw new ArgumentException("At least one report type must be specified", nameof(model.ReportTypes));
                 }
                
                 if (!Enum.IsDefined(typeof(Frequency), model.Frequency))
                 {
                    throw new ArgumentException("Invalid frequency value", nameof(model.Frequency));
                 }
                
                if (model.StartDate >= normalizedEndDate)
                {
                    throw new ArgumentException("Start date must be earlier than end date", nameof(model.StartDate));
                }

                var message = new Message<string, object>
                {
                    Key = model.FacilityId,
                    Headers = headers,
                    Value = new ReportScheduledMessage()
                    {
                        ReportTypes = model.ReportTypes,
                        Frequency = model.Frequency.ToString(),
                        StartDate = model.StartDate,
                        EndDate = normalizedEndDate,
                        ReportTrackingId = correlationId
                    }
                };

                await _producer.ProduceAsync(nameof(KafkaTopic.ReportScheduled), message);
                _logger.LogKafkaProducerReportScheduled(correlationId);

                return correlationId;

            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                _logger.LogKafkaProducerException(correlationId, ex.Message);
                throw;
            }

        }
    }
}
