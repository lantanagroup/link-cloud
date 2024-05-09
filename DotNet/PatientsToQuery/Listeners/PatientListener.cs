using Streamiz.Kafka.Net.SerDes;
using Streamiz.Kafka.Net.Stream;
using Streamiz.Kafka.Net;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using LantanaGroup.Link.PatientsToQuery.Application.Models;
using LantanaGroup.Link.PatientsToQuery.Serializers;
using Hl7.Fhir.Model;
using MongoDB.Driver;
using Newtonsoft.Json;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models;
using static PatientsToQuery.Settings.PatientsToQueryConstants;
using PatientsToQuery.Settings;

namespace LantanaGroup.Link.PatientsToQuery.Listeners
{
    public class PatientListener : BackgroundService
    {
        private readonly IOptions<KafkaConnection> _kafkaConnection;
        private readonly ILogger<PatientListener> _logger;

        public PatientListener(IOptions<KafkaConnection> kafkaConnection, ILogger<PatientListener> logger)
        {
            _kafkaConnection = kafkaConnection ?? throw new ArgumentException(nameof(kafkaConnection));
            _logger = logger;
        }

        protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var config = new StreamConfig<StringSerDes, StringSerDes>()
            {
                ApplicationId = ServiceName,
                BootstrapServers = _kafkaConnection.Value.BootstrapServers.First(),
                AutoOffsetReset = AutoOffsetReset.Earliest,
                Guarantee = ProcessingGuarantee.EXACTLY_ONCE,
                DeserializationExceptionHandler = (p, r, e) => ExceptionHandlerResponse.CONTINUE
            };

            StreamBuilder builder = new StreamBuilder();

            var admitStream = builder.Stream<string, List>(KafkaTopic.PatientIDsAcquired.ToString(), new StringSerDes(), new PatientIdsAcquiredSerializer<List>());
            var queriedStream = builder.Stream<string, DataAcquisitionRequestedValue>(KafkaTopic.DataAcquisitionRequested.ToString(), new StringSerDes(), new JsonSerializer<DataAcquisitionRequestedValue>());

            admitStream
                .FlatMapValues((k, v) =>
                {
                    return v.Entry;
                })
                .MapValues(v => { 
                    //Tag incoming PatientIDAcquired patients as 'admit'
                    _logger.LogInformation(new EventId(LoggingIds.GenerateItems, "Generating Admit PatientStatus"), "Patient {0} admitted", v.Item.Reference);
                    return new PatientStatus() { PatientId = v.Item.Reference, Status = Status.Admit }; 
                })
                .Merge(
                    //Tag incoming DataAcquisitionRequested patients as 'queried'
                    queriedStream
                        .Filter((k, v) => v.QueryType == QueryTypes.Initial.ToString())
                        .MapValues(v => {
                            _logger.LogInformation(new EventId(LoggingIds.GenerateItems, "Generating Queried PatientStatus"), "Patient {0} queried", v.PatientId);
                            return new PatientStatus() { PatientId = v.PatientId, Status = Status.Queried }; 
                        })
                )
                .GroupByKey()
                .Aggregate(() => "", (k, v, current) => {
                    //If the facility does not have an existing PatientsToQueryList, create a new one
                    if (string.IsNullOrWhiteSpace(current))
                    {
                        _logger.LogInformation(new EventId(LoggingIds.GenerateItems, "Generating new List"), "Creating new PatientsToQueryList for facility {0}", k);
                        var newVal = new PatientsToQueryValue()
                        {
                            PatientIds = new List<string>()
                        };

                        if (v.Status == Status.Admit)
                        {
                            newVal.PatientIds.Add(v.PatientId);
                        }

                        return JsonConvert.SerializeObject(newVal);
                    }

                    var val = JsonConvert.DeserializeObject<PatientsToQueryValue>(current);

                    _logger.LogInformation(new EventId(LoggingIds.ListItems, "Current List"), "Current PatientsToQueryList for facility {0}: {1}", k, string.Join(',', val.PatientIds));
                    if (v.Status == Status.Admit)
                    {
                        if (!val.PatientIds.Exists(x => x == v.PatientId))
                        {
                            val.PatientIds.Add(v.PatientId);
                        }
                    }
                    else
                    {
                        val.PatientIds.RemoveAll(x => x == v.PatientId);
                    }

                    _logger.LogInformation(new EventId(LoggingIds.ListItems, "Updated List"), "PatientsToQueryList changed for facility {0}: {1}", k, string.Join(',', val.PatientIds));

                    return JsonConvert.SerializeObject(val);                   
                })
                .ToStream()
                .To("PatientsToQuery", new StringSerDes(), new StringSerDes());
            

            Topology t = builder.Build();
            KafkaStream stream = new KafkaStream(t, config);

            Console.CancelKeyPress += (o, e) => {
                stream.Dispose();
            };

            await stream.StartAsync();
        }
    }
}
