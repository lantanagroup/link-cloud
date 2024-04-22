using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Shared.Application.Factories;
public class KafkaProducerFactory<TProducerKey, TProducerValue> : IKafkaProducerFactory<TProducerKey, TProducerValue>
{
    public KafkaConnection KafkaConnection { get => _kafkaConnection.Value; }
    private readonly ILogger<KafkaProducerFactory<TProducerKey, TProducerValue>> _logger;
    private readonly IOptions<KafkaConnection> _kafkaConnection;

    public KafkaProducerFactory(ILogger<KafkaProducerFactory<TProducerKey, TProducerValue>> logger, IOptions<KafkaConnection> kafkaConnection)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kafkaConnection = kafkaConnection ?? throw new ArgumentNullException(nameof(kafkaConnection));
    }

    public IProducer<string, AuditEventMessage> CreateAuditEventProducer(bool useOpenTelemetry = true)
    {
        try
        {
            return useOpenTelemetry ?
                new ProducerBuilder<string, AuditEventMessage>(_kafkaConnection.Value.CreateProducerConfig()).SetValueSerializer(new JsonWithFhirMessageSerializer<AuditEventMessage>()).BuildWithInstrumentation() :
                new ProducerBuilder<string, AuditEventMessage>(_kafkaConnection.Value.CreateProducerConfig()).SetValueSerializer(new JsonWithFhirMessageSerializer<AuditEventMessage>()).Build();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create AuditEvent Kafka producer.");
            throw;
        }
    }

    public IProducer<TProducerKey, TProducerValue> CreateProducer(ProducerConfig config, ISerializer<TProducerKey>? keySerializer = null, ISerializer<TProducerValue>? valueSerializer = null, bool useOpenTelemetry = true)
    {
        try
        {
            config.BootstrapServers = string.Join(", ", _kafkaConnection.Value.BootstrapServers);
            config.ReceiveMessageMaxBytes = _kafkaConnection.Value.ReceiveMessageMaxBytes;
            config.ClientId = _kafkaConnection.Value.ClientId;

            if (_kafkaConnection.Value.SaslProtocolEnabled)
            {
                config.SecurityProtocol = SecurityProtocol.SaslSsl;
                config.SaslMechanism = SaslMechanism.Plain;
                config.SaslUsername = _kafkaConnection.Value.SaslUsername;
                config.SaslPassword = _kafkaConnection.Value.SaslPassword;
                config.ApiVersionRequest = _kafkaConnection.Value.ApiVersionRequest;
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
            _logger.LogError(ex, "Failed to create Kafka producer.");
            throw;
        }
    }
}
