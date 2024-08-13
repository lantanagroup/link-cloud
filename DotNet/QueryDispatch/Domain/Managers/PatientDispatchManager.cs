﻿using Confluent.Kafka;
using KellermanSoftware.CompareNetObjects;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.QueryDispatch.Presentation.Services;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using Quartz;
using QueryDispatch.Application.Settings;

namespace QueryDispatch.Domain.Managers
{

    public interface IPatientDispatchManager
    {
        public  Task<string> createPatientDispatch(PatientDispatchEntity patientDispatch);
        public  Task<bool> deletePatientDispatch(string facilityId, string patientId);
    }

    public class PatientDispatchManager : IPatientDispatchManager
    {
        private readonly IEntityRepository<PatientDispatchEntity> _repository;
        private readonly ILogger<QueryDispatchConfigurationManager> _logger;
        private readonly IKafkaProducerFactory<string, AuditEventMessage> _kafkaProducerFactory;
        private readonly CompareLogic _compareLogic;
        private readonly ISchedulerFactory _schedulerFactory;

        public PatientDispatchManager(ILogger<QueryDispatchConfigurationManager> logger, IDatabase database, IKafkaProducerFactory<string, AuditEventMessage> kafkaProducerFactory, ISchedulerFactory schedulerFactory)
        {
            _repository = database.PatientDispatchRepo;
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
            _logger = logger;
            _compareLogic = new CompareLogic();
            _compareLogic.Config.MaxDifferences = 25;
            _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
        }

       

        public async Task<string> createPatientDispatch(PatientDispatchEntity patientDispatch)
        {
            try
            {
                //_datastore.Add(patientDispatch);
                await _repository.AddAsync(patientDispatch);

                _logger.LogInformation($"Created patient dispatch for patient id {patientDispatch.PatientId} in facility {patientDispatch.FacilityId}");

                await ScheduleService.CreateJobAndTrigger(patientDispatch, await _schedulerFactory.GetScheduler());


                using (var producer = _kafkaProducerFactory.CreateAuditEventProducer())
                {
                    var headers = new Headers
                    {
                        { "X-Correlation-Id", Guid.NewGuid().ToByteArray() }
                    };

                    var auditMessage = new AuditEventMessage
                    {
                        FacilityId = patientDispatch.FacilityId,
                        ServiceName = QueryDispatchConstants.ServiceName,
                        Action = AuditEventType.Create,
                        EventDate = DateTime.UtcNow,
                        Resource = typeof(PatientDispatchEntity).Name,
                        Notes = $"Created patient dispatch for patient id {patientDispatch.PatientId} in facility {patientDispatch.FacilityId}"
                    };

                    producer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
                    {
                        Value = auditMessage,
                        Headers = headers
                    });

                    producer.Flush();
                }
                return patientDispatch.FacilityId;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Create patient dispatch exception for patient id {patientDispatch.PatientId} in facility {patientDispatch.FacilityId}.", ex);
                throw new ApplicationException($"Failed to create patient dispatch record for patient id {patientDispatch.PatientId} in facility {patientDispatch.FacilityId}.");
            }
        }

        public async Task<bool> deletePatientDispatch(string facilityId, string patientId)
        {
            try
            {
                var entity = await _repository.FirstOrDefaultAsync(x => x.FacilityId == facilityId && x.PatientId == patientId);
                if (entity != null)
                {
                    await _repository.RemoveAsync(entity);
                }
                _logger.LogInformation($"Deleted Patient Dispatch record for patient id {patientId} in facility {facilityId}");

                using (var producer = _kafkaProducerFactory.CreateAuditEventProducer())
                {
                    var headers = new Headers
                        {
                            { "X-Correlation-Id", Guid.NewGuid().ToByteArray() }
                        };

                    var auditMessage = new AuditEventMessage
                    {
                        FacilityId = facilityId,
                        ServiceName = QueryDispatchConstants.ServiceName,
                        Action = AuditEventType.Delete,
                        EventDate = DateTime.UtcNow,
                        Resource = typeof(PatientDispatchEntity).Name,
                        Notes = $"Deleted Patient Dispatch record for patient id {patientId} in facility {facilityId}"
                    };

                    producer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
                    {
                        Value = auditMessage,
                        Headers = headers
                    });

                    producer.Flush();
                }

                return true;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Patient dispatch delete exception for patientId {patientId} in facility {facilityId}", patientId, facilityId);

                return false;
            }
        }
    }
}