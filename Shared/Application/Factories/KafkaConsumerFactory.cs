﻿using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZstdSharp.Unsafe;

namespace LantanaGroup.Link.Shared.Application.Factories;
public class KafkaConsumerFactory<TConsumerKey, TConsumerValue> : IKafkaConsumerFactory<TConsumerKey, TConsumerValue>
{
    private readonly ILogger<KafkaConsumerFactory<TConsumerKey, TConsumerValue>> _logger;
    private readonly IOptions<KafkaConnection> _kafkaConnection;

    public KafkaConsumerFactory(ILogger<KafkaConsumerFactory<TConsumerKey, TConsumerValue>> logger, IOptions<KafkaConnection> kafkaConnection)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kafkaConnection = kafkaConnection ?? throw new ArgumentNullException(nameof(kafkaConnection));
    }

    public IConsumer<TConsumerKey, TConsumerValue> CreateConsumer(ConsumerConfig config, IDeserializer<TConsumerKey>? keyDeserializer = null, IDeserializer<TConsumerValue>? valueDeserializer = null) 
    {
        try
        {
            if (string.IsNullOrWhiteSpace(config.GroupId))
            {
                throw new ArgumentException("No Kafka Group Id set in consumer configuration");
            }

            config.BootstrapServers = string.Join(", ", _kafkaConnection.Value.BootstrapServers);
            config.ReceiveMessageMaxBytes = _kafkaConnection.Value.ReceiveMessageMaxBytes;
            config.ClientId = _kafkaConnection.Value.ClientId;
            config.GroupId = _kafkaConnection.Value.GroupId;

            if (_kafkaConnection.Value.SaslProtocolEnabled)
            {
                config.SecurityProtocol = SecurityProtocol.SaslSsl;
                config.SaslMechanism = SaslMechanism.Plain;
                config.SaslUsername = _kafkaConnection.Value.SaslUsername;
                config.SaslPassword = _kafkaConnection.Value.SaslPassword;
                config.ApiVersionRequest = _kafkaConnection.Value.ApiVersionRequest;
            }

            var consumerBuilder = new ConsumerBuilder<TConsumerKey, TConsumerValue>(config);

            if (typeof(TConsumerKey) != typeof(string))
            {
                consumerBuilder.SetKeyDeserializer(keyDeserializer ?? new JsonWithFhirMessageDeserializer<TConsumerKey>());
            }

            if (typeof(TConsumerValue) != typeof(string))
            {
                consumerBuilder.SetValueDeserializer(valueDeserializer ?? new JsonWithFhirMessageDeserializer<TConsumerValue>());
            }

            return consumerBuilder.Build();
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to create " + config.GroupId + " Kafka consumer.", ex);
            throw new Exception("Failed to create " + config.GroupId + " Kafka consumer.");
        }
    }
}
