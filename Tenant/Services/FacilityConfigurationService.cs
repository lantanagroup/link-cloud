using MongoDB.Driver;
using LantanaGroup.Link.Tenant.Entities;
using static LantanaGroup.Link.Tenant.Entities.ScheduledTaskModel;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Tenant.Repository;
using Confluent.Kafka;
using System.Text;
using LantanaGroup.Link.Tenant.Utils;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.Options;
using LantanaGroup.Link.Tenant.Models;

namespace LantanaGroup.Link.Tenant.Services
{

    public class FacilityConfigurationService
    {

        private readonly ILogger<FacilityConfigurationService> _logger;
        private readonly HttpClient _httpClient;
        private static List<KafkaTopic> _topics = new List<KafkaTopic>();
        private readonly IKafkaProducerFactory<string, object> _kafkaProducerFactory;
        private readonly IOptions<TenantConfig> _tenantConfig;


        IFacilityConfigurationRepo _facilityConfigurationRepo;

        static FacilityConfigurationService()
        {
            _topics.Add(KafkaTopic.RetentionCheckScheduled);
            _topics.Add(KafkaTopic.ReportScheduled);
        }


        public FacilityConfigurationService(IFacilityConfigurationRepo facilityConfigurationRepo, ILogger<FacilityConfigurationService> logger, IKafkaProducerFactory<string, object> kafkaProducerFactory, IOptions<TenantConfig> tenantConfig, HttpClient httpClient)
        {
            _facilityConfigurationRepo = facilityConfigurationRepo;
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
            _tenantConfig = tenantConfig ?? throw new ArgumentNullException(nameof(tenantConfig));
            _logger = logger;   
            _httpClient = httpClient;
        }

        public IFacilityConfigurationRepo getFacilityConfigurationRepo()
        {
            return _facilityConfigurationRepo != null ? _facilityConfigurationRepo : null;
        }

        public async Task<List<FacilityConfigModel>> GetFacilities(CancellationToken cancellationToken)
        {
           var result =  await _facilityConfigurationRepo.GetAsync(cancellationToken);

           return result;
        }

        public async Task<List<FacilityConfigModel>> GetFacilitiesByFilters(CancellationToken cancellationToken, FilterDefinition<FacilityConfigModel> filter)
        {
            return await _facilityConfigurationRepo.FindAsync(filter, cancellationToken);
        }

        public async Task<bool> CreateFacility(FacilityConfigModel newFacility, CancellationToken cancellationToken)
        {
            this.ValidateFacility(newFacility);

            var facility = await _facilityConfigurationRepo.GetAsyncByFacilityId(newFacility.FacilityId, cancellationToken);

            // validates facility does not exist
            if (facility is not null)
            {
                this._logger.LogError($"Facility {newFacility.FacilityId} already exists");

                throw new ApplicationException($"Facility {newFacility.FacilityId} already exists");
            }

            this.ValidateSchedules(newFacility);

            bool created = false;

            try
            {
                created = await _facilityConfigurationRepo.CreateAsync(newFacility, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Facility {newFacility.FacilityId} failed to create. " + ex.Message);
            }

            using var producer = _kafkaProducerFactory.CreateAuditEventProducer();

            // audit create facility event
            try
            {
                AuditEventMessage auditEvent;
                Headers headers;
                Helper.CreateFacilityAuditEvent(newFacility, out auditEvent, out headers);
                // send the Audit Event

                await producer.ProduceAsync(KafkaTopic.AuditableEventOccurred.ToString(), new Message<string, AuditEventMessage>
                {
                    Key = newFacility.FacilityId,
                    Value = auditEvent,
                    Headers = headers
                });
               

            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to generate an audit event for create of facility configuration {newFacility.Id}.", ex);
            }

            return created;
        }

        public async Task<FacilityConfigModel> GetFacilityById(string id, CancellationToken cancellationToken)
        {
            var result = await _facilityConfigurationRepo.GetAsyncById(id, cancellationToken);

            return result;

        }

        public async Task<FacilityConfigModel> GetFacilityByFacilityId(string facilityId, CancellationToken cancellationToken)
        {
            var result = await _facilityConfigurationRepo.GetAsyncByFacilityId(facilityId, cancellationToken);

            return result;

        }


        public async Task<string> UpdateFacility(string id, FacilityConfigModel newFacility, CancellationToken cancellationToken = default)
        {
            this.ValidateFacility(newFacility);

            FacilityConfigModel existingFacility = _facilityConfigurationRepo.GetAsyncById(id, cancellationToken).Result;

            if (existingFacility is null)
            {
                this._logger.LogError($"Facility with Id: {id} Not Found");

                throw new ApplicationException($"Facility with Id: {id} Not Found");
            }

            FacilityConfigModel foundFacility = _facilityConfigurationRepo.GetAsyncByFacilityId(newFacility.FacilityId,cancellationToken).Result;

            if (foundFacility != null && foundFacility.Id != id)
            {
                this._logger.LogError($"Facility {newFacility.FacilityId} already exists");

                throw new ApplicationException($"Facility {newFacility.FacilityId} already exists");
            }

            this.ValidateSchedules(newFacility);

            _ = _facilityConfigurationRepo.UpdateAsync(id, newFacility, cancellationToken);

            // audit update facility event
            using var producer = _kafkaProducerFactory.CreateAuditEventProducer();
            try
            {
                AuditEventMessage auditEvent;
                Headers headers;
                Helper.UpdateFacilityAuditEvent(newFacility, existingFacility, out auditEvent, out headers);

                await producer.ProduceAsync(KafkaTopic.AuditableEventOccurred.ToString(), new Message<string, AuditEventMessage>
                {
                    Key = newFacility.FacilityId,
                    Value = auditEvent,
                    Headers = headers
                });

            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to generate an audit event for update of facility configuration {newFacility.Id}.", ex);
            }

            return id;
        }

        public async Task<string> RemoveFacility(string facilityId, CancellationToken cancellationToken)
        {
            // validate facility exists
            var existingFacility = _facilityConfigurationRepo.GetAsyncByFacilityId(facilityId, cancellationToken).Result;

            if (existingFacility is null)
            {
                this._logger.LogError($"Facility with Id: {facilityId} Not Found");
                throw new ApplicationException($"Facility with Id: {facilityId} Not Found");
            }

            await _facilityConfigurationRepo.RemoveAsync(existingFacility.Id, cancellationToken);

            using var producer = _kafkaProducerFactory.CreateAuditEventProducer();

            // audit delete facility event
            try
            {
                AuditEventMessage auditEvent;
                Headers headers;
                Helper.DeleteFacilityAuditEvent(existingFacility, out auditEvent, out headers);

                await producer.ProduceAsync(KafkaTopic.AuditableEventOccurred.ToString(), new Message<string, AuditEventMessage>
                {
                    Key = existingFacility.FacilityId,
                    Value = auditEvent,
                    Headers = headers
                });

            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to generate an audit event for delete of facility configuration {existingFacility.Id}.", ex);
            }

            return facilityId;
        }

        private void ValidateFacility(FacilityConfigModel facility)
        {
            StringBuilder validationErrors = new StringBuilder();

            if (string.IsNullOrWhiteSpace(facility.FacilityId))
            {
                validationErrors.AppendLine("FacilityId must be entered.");              
            }
            if (string.IsNullOrWhiteSpace(facility.FacilityName))
            {
                validationErrors.AppendLine("FacilityName must be entered.");
            }
            if (!string.IsNullOrEmpty(validationErrors.ToString()))
            {
                throw new ApplicationException(validationErrors.ToString());
            }
            // validate monthly reporting plans fields
            if (facility.MonthlyReportingPlans != null)
            {
                foreach (MonthlyReportingPlanModel monthlyReportingPlan in facility.MonthlyReportingPlans)
                {
                    if (string.IsNullOrWhiteSpace(monthlyReportingPlan.ReportType))
                    {
                        validationErrors.AppendLine("ReportType for MonthlyReportingPlans must be entered.");
                    }
                    if (monthlyReportingPlan.ReportMonth == null)
                    {
                        validationErrors.AppendLine("ReportMonth for MonthlyReportingPlans must be entered.");
                    }
                    if (monthlyReportingPlan.ReportYear == null)
                    {
                        validationErrors.AppendLine("ReportYear for MonthlyReportingPlans must be entered.");
                    }
                }
            }

        }

        private void ValidateSchedules(FacilityConfigModel facility)
        {
            foreach (ScheduledTaskModel scheduledTask in facility.ScheduledTasks)
            {
                // validate topic
                if (!Helper.ValidateTopic(scheduledTask.KafkaTopic, _topics))
                {
                    throw new ApplicationException($"kafka topic {scheduledTask.KafkaTopic} is Invalid. Valid values are {string.Join(" and ", _topics.ToList())}");
                }
                // validate scheduleTrigger
                foreach (ScheduledTaskModel.ReportTypeSchedule reportTypeSchedule in scheduledTask.ReportTypeSchedules)
                {
                    foreach (string trigger in reportTypeSchedule.ScheduledTriggers)
                    {
                        if (!Helper.IsValidSchedule(trigger))
                        {
                            this._logger.LogError($"ScheduledTrigger {trigger} for facility {facility.FacilityId} and kafka topic {scheduledTask.KafkaTopic} is not valid chron expression");
                            throw new ApplicationException($"ScheduledTrigger {trigger} for facility {facility.FacilityId} and kafka topic {scheduledTask.KafkaTopic} is not valid chron expression");
                        }
                    }
                }
            }
            // validate topic is unique within Facility

            IEnumerable<string> duplicates = facility.ScheduledTasks.GroupBy(i => i.KafkaTopic).Where(g => g.Count() > 1).Select(g => g.Key);

            string duplicatedTopics = string.Join(" ", duplicates.ToList());

            if (!duplicatedTopics.Equals(""))
            {  
                this._logger.LogError($"The following topics {duplicatedTopics} are duplicated for facility {facility.FacilityId} ");
                throw new ApplicationException($"The following topics {duplicatedTopics} are duplicated for facility {facility.FacilityId} ");
            }
            // validate report Type within Topic
            StringBuilder schedulingErrors = new StringBuilder();
            foreach (ScheduledTaskModel facilityScheduledTask in facility.ScheduledTasks)
            {
               
                IEnumerable<string> duplicatedReportTypes = facilityScheduledTask.ReportTypeSchedules.GroupBy(i => i.ReportType).Where(g => g.Count() > 1).Select(g => g.Key);

                string duplicatedReportTypesString = string.Join(" ", duplicatedReportTypes.ToList());

                if (!duplicatedReportTypesString.Equals(""))
                {
                    this._logger.LogError($"The following ReportTypes {duplicatedReportTypesString} are duplicated for facility {facility.FacilityId} and KafakaTopic {facilityScheduledTask.KafkaTopic}");
                    throw new ApplicationException($"The following ReportTypes {duplicatedReportTypesString} are duplicated for facility {facility.FacilityId} and KafakaTopic {facilityScheduledTask.KafkaTopic}");
                }
                int i = 1;
                foreach (ReportTypeSchedule reportTypeSchedule in facilityScheduledTask.ReportTypeSchedules)
                {
                    // validate reportType not null and report trigers not emty            
                    if (string.IsNullOrWhiteSpace(reportTypeSchedule.ReportType))
                    {
                        schedulingErrors.AppendLine($"Report Type under KafkaTopic {facilityScheduledTask.KafkaTopic} and ReportTypeSchedule Number {i} must be specified.");
                    }
                    // validate the reportype is one of the known values
                    if (!MeasureDefinitionTypeValidation.Validate(reportTypeSchedule.ReportType))
                    {
                        throw new ApplicationException($"ReportType {reportTypeSchedule.ReportType} is not a known report type.");
                    }
                    // check if the report type was set-up in Measure Evaluation Service

                    string requestUrl = _tenantConfig.Value.MeasureDefUrl + $"/{reportTypeSchedule.ReportType}";

                    var response = _httpClient.GetAsync(requestUrl).Result;

                    // check respone status code
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new ApplicationException($"Report Type {reportTypeSchedule.ReportType} is not setup in MeasureEval service.");
                    }

                    if (reportTypeSchedule.ScheduledTriggers.Count == 0)
                    {
                        schedulingErrors.AppendLine($"Scheduled triggers under KafkaTopic {facilityScheduledTask.KafkaTopic} and ReportTypeSchedule Number {i} must be specified.");
                    }

                    i++;

                    IEnumerable<string> duplicatedTriggers = reportTypeSchedule.ScheduledTriggers.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key);

                    string duplicatedTriggersString = string.Join(" ", duplicatedTriggers.ToList());

                    if (!duplicatedTriggersString.Equals(""))
                    {
                        this._logger.LogError($"The following Trigger {duplicatedTriggersString} are duplicated for facility {facility.FacilityId} and KafakaTopic {facilityScheduledTask.KafkaTopic} and reportType {reportTypeSchedule.ReportType}");
                        throw new ApplicationException($"The following Trigger {duplicatedTriggersString} are duplicated for facility {facility.FacilityId} and KafakaTopic {facilityScheduledTask.KafkaTopic} and reportType {reportTypeSchedule.ReportType}");
                    }
                  
                }             
            }
            if (!string.IsNullOrEmpty(schedulingErrors.ToString()))
            {
                throw new ApplicationException(schedulingErrors.ToString());
            }

        }
    }
}
