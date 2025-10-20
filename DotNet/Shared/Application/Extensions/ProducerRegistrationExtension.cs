using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.DependencyInjection;

namespace LantanaGroup.Link.Shared.Application.Extensions;
public static class ProducerRegistrationExtension
{
    public static void RegisterKafkaProducer<TProducerKey, TProducerValue>(
        this IServiceCollection services,
        KafkaConnection kafkaConnection,
        ProducerConfig config, 
        ISerializer<TProducerKey>? keySerializer = null, 
        ISerializer<TProducerValue>? valueSerializer = null, 
        bool useOpenTelemetry = true)
    {
        var producer = new KafkaProducerFactory<TProducerKey, TProducerValue>(kafkaConnection).CreateProducer(config, keySerializer, valueSerializer, useOpenTelemetry);
        services.AddSingleton(producer);
        
    }
}
