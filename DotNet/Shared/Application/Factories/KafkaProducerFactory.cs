using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.Shared.Application.Factories;
public class KafkaProducerFactory<TProducerKey, TProducerValue> : IKafkaProducerFactory<TProducerKey, TProducerValue>
{
    public KafkaConnection KafkaConnection { get => _kafkaConnection; }
    private readonly KafkaConnection _kafkaConnection;

    public KafkaProducerFactory(KafkaConnection kafkaConnection)
    {
        _kafkaConnection = kafkaConnection ?? throw new ArgumentNullException(nameof(kafkaConnection));
    }

    public IProducer<string, AuditEventMessage> CreateAuditEventProducer(bool useOpenTelemetry = true)
    {
        try
        {
            return useOpenTelemetry ?
                new ProducerBuilder<string, AuditEventMessage>(_kafkaConnection.CreateProducerConfig()).SetValueSerializer(new JsonWithFhirMessageSerializer<AuditEventMessage>()).BuildWithInstrumentation() :
                new ProducerBuilder<string, AuditEventMessage>(_kafkaConnection.CreateProducerConfig()).SetValueSerializer(new JsonWithFhirMessageSerializer<AuditEventMessage>()).Build();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public IProducer<TProducerKey, TProducerValue> CreateProducer(ProducerConfig config, ISerializer<TProducerKey>? keySerializer = null, ISerializer<TProducerValue>? valueSerializer = null, bool useOpenTelemetry = true)
    {
        try
        {
            config.BootstrapServers = string.Join(", ", _kafkaConnection.BootstrapServers);
            config.ReceiveMessageMaxBytes = _kafkaConnection.ReceiveMessageMaxBytes;
            config.ClientId = _kafkaConnection.ClientId;

            if (_kafkaConnection.SaslProtocolEnabled)
            {
                config.SecurityProtocol = SecurityProtocol.SaslPlaintext;
                config.SaslMechanism = SaslMechanism.Plain;
                config.SaslUsername = _kafkaConnection.SaslUsername;
                config.SaslPassword = _kafkaConnection.SaslPassword;
            }

            var producerBuilder = new ProducerBuilder<TProducerKey, TProducerValue>(config);

            if (typeof(TProducerKey) != typeof(string))
            {
                producerBuilder.SetKeySerializer(keySerializer ?? new JsonWithFhirMessageSerializer<TProducerKey>());
            }

            if (typeof(TProducerValue) != typeof(string))
            {
                producerBuilder.SetValueSerializer(valueSerializer ?? new JsonWithFhirMessageSerializer<TProducerValue>());
            }

            return useOpenTelemetry ? producerBuilder.BuildWithInstrumentation() : producerBuilder.Build();

        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
