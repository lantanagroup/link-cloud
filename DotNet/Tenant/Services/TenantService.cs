using Confluent.Kafka;
using Grpc.Core;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Tenant.Services;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Tenant
{
    public class TenantService : TenantSvc.TenantSvcBase
    {
        private readonly ILogger<TenantService> _logger;
        private readonly IOptions<KafkaConnection> _kafkaConnection;

        public TenantService(ILogger<TenantService> logger, IOptions<KafkaConnection> kafkaConnection, FacilityConfigurationService tenantConfigurationService)
        {
            this._logger = logger;
            this._kafkaConnection = kafkaConnection;
        }

        public override async Task<GetFhirConnectionResponse> GetFhirConnection(GetFhirConnectionRequest request, ServerCallContext context)
        {
            //TODO: add code to handle this request once requirements are finalized.
            throw new RpcException(new Status(StatusCode.Unimplemented, ""));
        }

        public override async  Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {
            Patient newPatient = new Patient();
            newPatient.Active = true;
            newPatient.Id = "test";
            Bundle newBundle = new Bundle();
            newBundle.Entry.Add(new Bundle.EntryComponent()
            {
                Resource = newPatient,
                Request = new Bundle.RequestComponent()
                {
                    Method = Bundle.HTTPVerb.PUT,
                    Url = "Patient/" + newPatient.Id
                }
            });

            var serializer = new FhirJsonSerializer();

            using (var producer = new ProducerBuilder<Null, string>(this._kafkaConnection.Value.CreateProducerConfig()).Build())
            {
                this._logger.LogInformation("Produce Event!");
                producer.Produce("my-topic", new Message<Null, string> { Value = request.Name });
            }

            HelloReply reply = new HelloReply
            {
                Message = "Hello " + request.Name,
                SomeOtherProperty = "Test",
                Bundle = serializer.SerializeToString(newBundle)
            };

            //return await System.Threading.Tasks.Task.FromResult(reply);
            return reply;
        }
    }
}