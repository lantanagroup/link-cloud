﻿using LantanaGroup.Link.Tenant.Entities;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using LantanaGroup.Link.Tenant.Models.Messages;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Tenant.Listeners
{
    public class ListenerXX : IHostedService
    {
        private const string DbName = "tenant";
        private const string CollectionName = "tenants";
 
        private readonly ILogger<ListenerXX> _logger;
        private readonly IOptions<KafkaConnection> _kafkaConnection;
        private readonly IOptions<MongoConnection> _mongoConnection;
        internal IConsumer<ReportScheduledKey, ReportScheduledMessage> _consumer;

        public ListenerXX(ILogger<ListenerXX> logger, IOptions<KafkaConnection> kafkaConnection, IOptions<MongoConnection> mongoConnection)
        {
            _logger = logger;
            _kafkaConnection = kafkaConnection;
            _mongoConnection = mongoConnection;
            ConsumerBuilder<ReportScheduledKey, ReportScheduledMessage> consumerBuilder = new ConsumerBuilder<ReportScheduledKey, ReportScheduledMessage>(kafkaConnection.Value.CreateConsumerConfig())
             .SetKeyDeserializer(new JsonWithFhirMessageDeserializer<ReportScheduledKey>())
             .SetValueDeserializer(new JsonWithFhirMessageDeserializer<ReportScheduledMessage>());
            this._consumer = consumerBuilder.Build();

        }

        public async Task Start()
        {
            
            {
                this._consumer.Subscribe(KafkaTopic.ReportScheduled.ToString());

                
                bool cancelled = false;

                while (!cancelled)
                {
                    var consumeResult = this._consumer.Consume();

                    ReportScheduledMessage message = consumeResult.Message.Value;

                    ReportScheduledKey key = consumeResult.Message.Key;

                    if (consumeResult != null)
                    {
                        this._logger.LogInformation($"Consume Event for: {key.FacilityId} {key.ReportType}  ");
                        foreach (KeyValuePair<string, object> parameter in message.Parameters)
                        {
                            Console.WriteLine(parameter.Key + ":" + parameter.Value);
                        }

                    }
                }

                this._consumer.Close();
            }
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Task.Factory.StartNew(() => this.Start());
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

    }
}
