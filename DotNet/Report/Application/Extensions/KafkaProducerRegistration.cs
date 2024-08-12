using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;

namespace LantanaGroup.Link.Report.Application.Extensions;

public static class KafkaProducerRegistration
{
    public static void RegisterKafkaProducers(this IServiceCollection services, KafkaConnection kafkaConnection)
    {
        services.AddTransient<IKafkaProducerFactory<string, DataAcquisitionRequestedValue>, KafkaProducerFactory<string, DataAcquisitionRequestedValue>>();
        services.AddTransient<IKafkaProducerFactory<SubmissionReportKey, SubmissionReportValue>, KafkaProducerFactory<SubmissionReportKey, SubmissionReportValue>>();
        services.AddTransient<IKafkaProducerFactory<string, string>, KafkaProducerFactory<string, string>>();

        var dataAcqProducerConfig = new ProducerConfig()
        {
            ClientId = "Report_DataAcquisitionScheduled"
        };

        var dataAcqProducer = new KafkaProducerFactory<string, DataAcquisitionRequestedValue>(kafkaConnection).CreateProducer(dataAcqProducerConfig);
        services.AddSingleton(dataAcqProducer);

        var submissionProducerConfig = new ProducerConfig()
        {
            ClientId = "Report_SubmissionReportScheduled"
        };
        var submissionProducer = new KafkaProducerFactory<SubmissionReportKey, SubmissionReportValue>(kafkaConnection).CreateProducer(submissionProducerConfig);
        services.AddSingleton(submissionProducer);


    }
}
