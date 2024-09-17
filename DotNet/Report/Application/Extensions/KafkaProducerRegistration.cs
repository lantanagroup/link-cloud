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
        services.AddTransient<IKafkaProducerFactory<SubmitReportKey, SubmitReportValue>, KafkaProducerFactory<SubmitReportKey, SubmitReportValue>>();
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
        var submissionProducer = new KafkaProducerFactory<SubmitReportKey, SubmitReportValue>(kafkaConnection).CreateProducer(submissionProducerConfig);
        services.AddSingleton(submissionProducer);


    }
}
