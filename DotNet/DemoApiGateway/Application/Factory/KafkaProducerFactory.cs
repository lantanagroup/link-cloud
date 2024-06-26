﻿using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.DemoApiGateway.Application.models;
using LantanaGroup.Link.DemoApiGateway.Application.models.interfaces;
using LantanaGroup.Link.DemoApiGateway.Application.models.testing;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.DemoApiGateway.Application.Factory
{
    public class KafkaProducerFactory : IKafkaProducerFactory
    {
        private readonly ILogger<KafkaProducerFactory> _logger;
        private readonly IOptions<KafkaConnection> _kafkaConnection;

        public KafkaProducerFactory(ILogger<KafkaProducerFactory> logger, IOptions<KafkaConnection> kafkaConnection)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConnection = kafkaConnection ?? throw new ArgumentNullException(nameof(_kafkaConnection));
        }

        public IProducer<ReportScheduledKey, ReportScheduledMessage> CreateReportScheduledProducer(string clientId)
        {
            KafkaConnection kafkaConnection = new KafkaConnection();
            kafkaConnection.BootstrapServers = _kafkaConnection.Value.BootstrapServers;
            kafkaConnection.ClientId = clientId;

            try
            {
                return new ProducerBuilder<ReportScheduledKey, ReportScheduledMessage>(kafkaConnection.CreateProducerConfig()).SetKeySerializer(new JsonWithFhirMessageSerializer<ReportScheduledKey>()).SetValueSerializer(new JsonWithFhirMessageSerializer<ReportScheduledMessage>()).BuildWithInstrumentation();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create Demo Api Gateway Report Scheduled Kafka producer.", ex);
                throw new Exception("Failed to create Demo Api Gateway Report Scheduled Kafka producer.");
            }

        }

        public IProducer<string, PatientEventMessage> CreatePatientEventProducer(string clientId)
        {
            KafkaConnection patientEventKafkaConnection = new KafkaConnection();
            patientEventKafkaConnection.BootstrapServers = _kafkaConnection.Value.BootstrapServers;
            patientEventKafkaConnection.ClientId = clientId;

            try 
            {
                return new ProducerBuilder<string, PatientEventMessage>(patientEventKafkaConnection.CreateProducerConfig()).SetValueSerializer(new JsonWithFhirMessageSerializer<PatientEventMessage>()).BuildWithInstrumentation();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create Demo Api Gateway Patient Event Kafka producer.", ex);
                throw new Exception("Failed to create Demo Api Gateway Patient Event Kafka producer.");
            }
            
        }

        public IProducer<string, DataAcquisitionRequestedMessage> CreateDataAcquisitionRequestedProducer(string clientId)
        {
            KafkaConnection patientEventKafkaConnection = new KafkaConnection();
            patientEventKafkaConnection.BootstrapServers = _kafkaConnection.Value.BootstrapServers;
            patientEventKafkaConnection.ClientId = clientId;

            try
            {
                return new ProducerBuilder<string, DataAcquisitionRequestedMessage>(patientEventKafkaConnection.CreateProducerConfig()).SetValueSerializer(new JsonWithFhirMessageSerializer<DataAcquisitionRequestedMessage>()).BuildWithInstrumentation();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create Demo Api Gateway Data Acquisition Requested Kafka producer.", ex);
                throw new Exception("Failed to create Demo Api Gateway Data Acquisition Requested Kafka producer.");
            }
        }
    }
}
