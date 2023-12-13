using Streamiz.Kafka.Net.SerDes;
using Streamiz.Kafka.Net.Stream;
using Streamiz.Kafka.Net;
using Confluent.Kafka;
using LantanaGroup.Link.PatientBundlePerMeasure.Serializers;
using Microsoft.Extensions.Options;
using LantanaGroup.Link.Shared.Configs;
using LantanaGroup.Link.Shared.Models;
using LantanaGroup.Link.PatientBundlePerMeasure.Application.Models;
using System.Text;

namespace LantanaGroup.Link.PatientBundlePerMeasure.Listeners
{
    public class PatientNormalizedListener : BackgroundService
    {
        private readonly IOptions<KafkaConnection> _kafkaConnection;

        public PatientNormalizedListener(IOptions<KafkaConnection> kafkaConnection) 
        {
            _kafkaConnection = kafkaConnection ?? throw new ArgumentException(nameof(kafkaConnection));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var config = new StreamConfig<StringSerDes, JsonSerializer<PatientNormalizedValue>>()
            {
                ApplicationId = "PatientBundlePerMeasure",
                BootstrapServers = _kafkaConnection.Value.BootstrapServers.First(),
                AutoOffsetReset = AutoOffsetReset.Earliest,
                Guarantee = ProcessingGuarantee.EXACTLY_ONCE
            };
            
            StreamBuilder builder = new StreamBuilder();

            var kstream = builder.Stream<string, PatientNormalizedValue>(KafkaTopic.PatientNormalized.ToString(), new StringSerDes(), new JsonSerializer<PatientNormalizedValue>());

            kstream
                .FlatMap((k, v) => {
                    var values = new List<KeyValuePair<string, BundleEvalRequestedValue>>();

                    foreach (var scheduledReport in v.ScheduledReports) 
                    {
                        var valuePair = new KeyValuePair<string, BundleEvalRequestedValue> (
                            k,
                            new BundleEvalRequestedValue()
                            {
                                Bundle = v.Bundle,
                                ScheduledReport = scheduledReport
                                
                            });

                        values.Add(valuePair);
                    }

                    return values;
                })
                .To(KafkaTopic.BundleEvalRequested.ToString(), new StringSerDes(), new JsonSerializer<BundleEvalRequestedValue>());

            Topology t = builder.Build();
            KafkaStream stream = new KafkaStream(t, config);

            Console.CancelKeyPress += (o, e) => {
                stream.Dispose();
            };

            await stream.StartAsync();
        }
    }  
}
