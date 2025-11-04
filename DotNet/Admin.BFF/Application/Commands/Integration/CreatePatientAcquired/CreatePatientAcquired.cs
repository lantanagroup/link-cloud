using Confluent.Kafka;
using Hl7.Fhir.Model;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration
{
    public class CreatePatientAcquired : ICreatePatientAcquired
    {
        private readonly ILogger<CreatePatientAcquired> _logger;
        private readonly IProducer<string, object> _producer;

        public CreatePatientAcquired(ILogger<CreatePatientAcquired> logger, IProducer<string, object> producer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        }

        public async Task<string> Execute(PatientAcquired model, string? userId = null, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Producing Patient Acquired Event");
            string correlationId = Guid.NewGuid().ToString();

            try
            {
                var headers = new Headers
                {
                    { "X-Correlation-Id", System.Text.Encoding.ASCII.GetBytes(correlationId) }
                };

                // create a list
                var patientList = new List{};

                // add entries to the patientList for each patient in model.PatientIds  
                for (int i = 0; i < model.PatientIds.Count; i++)
                {
                    var entry = new List.EntryComponent
                    {
                        Item = new ResourceReference("Patient/" + model.PatientIds[i]),
                    };
                    patientList.Entry.Add(entry);
                }


                // Output the FHIR List in JSON format
              //  var json = new Hl7.Fhir.Serialization.FhirJsonSerializer().SerializeToString(patientList);

                var message = new Message<string, object>
                {
                    Key = model.FacilityId,
                    Value = new PatientAcquiredMessage { PatientIds = patientList,  ReportTrackingId = model.ReportTrackingId},
                    Headers = headers
                };

                await _producer.ProduceAsync(nameof(KafkaTopic.PatientListsAcquired), message);

                return correlationId;

            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                _logger.LogKafkaProducerException(nameof(KafkaTopic.PatientListsAcquired), ex.Message);
                throw;
            }

        }
    }
}
