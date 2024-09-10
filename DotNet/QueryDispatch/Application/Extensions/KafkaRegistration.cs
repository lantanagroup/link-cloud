using LantanaGroup.Link.QueryDispatch.Application.Models;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace QueryDispatch.Application.Extensions;

public static class KafkaRegistration
{
    public static void RegisterKafka(this IServiceCollection services, KafkaConnection kafkaConnection)
    {
        services.AddTransient<IKafkaConsumerFactory<string, ReportScheduledValue>, KafkaConsumerFactory<string, ReportScheduledValue>>();
        services.AddTransient<IKafkaConsumerFactory<string, PatientEventValue>, KafkaConsumerFactory<string, PatientEventValue>>();
        services.AddTransient<IKafkaConsumerFactory<string, string>, KafkaConsumerFactory<string, string>>();

        services.AddTransient<IKafkaProducerFactory<string, DataAcquisitionRequestedValue>, KafkaProducerFactory<string, DataAcquisitionRequestedValue>>();
        var dataProducer = new KafkaProducerFactory<string, DataAcquisitionRequestedValue>(kafkaConnection).CreateProducer(new Confluent.Kafka.ProducerConfig());
        services.AddSingleton(dataProducer);

        services.AddTransient<IKafkaProducerFactory<string, AuditEventMessage>, KafkaProducerFactory<string, AuditEventMessage>>();
        var auditProducer = new KafkaProducerFactory<string, AuditEventMessage>(kafkaConnection).CreateAuditEventProducer();
        services.AddSingleton(auditProducer);

        services.AddTransient<IKafkaProducerFactory<string, string>, KafkaProducerFactory<string, string>>();
        var stringProducer = new KafkaProducerFactory<string, string>(kafkaConnection).CreateProducer(new Confluent.Kafka.ProducerConfig());
        services.AddSingleton(stringProducer);

        services.AddTransient<IKafkaProducerFactory<string, PatientEventValue>, KafkaProducerFactory<string, PatientEventValue>>();
        var patientProducer = new KafkaProducerFactory<string, PatientEventValue>(kafkaConnection).CreateProducer(new Confluent.Kafka.ProducerConfig());
        services.AddSingleton(patientProducer);

        services.AddTransient<IKafkaProducerFactory<string, ReportScheduledValue>, KafkaProducerFactory<string, ReportScheduledValue>>();
        var reportProducer = new KafkaProducerFactory<string, ReportScheduledValue>(kafkaConnection).CreateProducer(new Confluent.Kafka.ProducerConfig());
        services.AddSingleton(reportProducer);
    }
}