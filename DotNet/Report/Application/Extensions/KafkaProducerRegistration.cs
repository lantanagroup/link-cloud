using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Report.Application.Extensions;

public static class KafkaProducerRegistration
{
    public static void RegisterKafkaProducers(this IServiceCollection services, KafkaConnection kafkaConnection)
    {
        services.AddTransient<IKafkaProducerFactory<string, DataAcquisitionRequestedValue>, KafkaProducerFactory<string, DataAcquisitionRequestedValue>>();
        services.AddTransient<IKafkaProducerFactory<string, string>, KafkaProducerFactory<string, string>>();
        services.AddTransient<IKafkaProducerFactory<string, EvaluationRequestedValue>, KafkaProducerFactory<string, EvaluationRequestedValue>>();

        var dataAcqProducerConfig = new ProducerConfig()
        {
            ClientId = "Report_DataAcquisitionScheduled"
        };

        var dataAcqProducer = new KafkaProducerFactory<string, DataAcquisitionRequestedValue>(kafkaConnection).CreateProducer(dataAcqProducerConfig);
        services.AddSingleton(dataAcqProducer);

        var readyForValidationConfig = new ProducerConfig()
        {
            ClientId = "Report_ReadyForValidation"
        };
        var readyForValidationProducer = new KafkaProducerFactory<ReadyForValidationKey, ReadyForValidationValue>(kafkaConnection).CreateProducer(readyForValidationConfig);
        services.AddSingleton(readyForValidationProducer);

        var evaluationRequestedConfig = new ProducerConfig()
        {
            ClientId = "Report_EvaluationRequested"
        };
        var evaluationRequestedProducer = new KafkaProducerFactory<string, EvaluationRequestedValue>(kafkaConnection).CreateProducer(evaluationRequestedConfig);
        services.AddSingleton(evaluationRequestedProducer);

        var submitPayloadConfig = new ProducerConfig()
        {
            ClientId = "Report_SubmitPayload"
        };
        var submitPayloadProducer = new KafkaProducerFactory<SubmitPayloadKey, SubmitPayloadValue>(kafkaConnection).CreateProducer(submitPayloadConfig);
        services.AddSingleton(submitPayloadProducer);
    }
}
