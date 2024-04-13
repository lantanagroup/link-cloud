﻿using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Tenant.Commands;
using LantanaGroup.Link.Tenant.Config;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Repository.Interfaces.Sql;
using LantanaGroup.Link.Tenant.Utils;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Text;
using static LantanaGroup.Link.Tenant.Entities.ScheduledTaskModel;


namespace LantanaGroup.Link.Tenant.Services
{
    public class FacilityConfigurationService
    {

        private readonly ILogger<FacilityConfigurationService> _logger;
        private readonly HttpClient _httpClient;
        private static List<KafkaTopic> _topics = new List<KafkaTopic>();
        private readonly IKafkaProducerFactory<string, object> _kafkaProducerFactory;
        private readonly IOptions<MeasureApiConfig> _measureApiConfig;


        private readonly IFacilityConfigurationRepo _facilityConfigurationRepo;

        private readonly CreateAuditEventCommand _createAuditEventCommand;

        static FacilityConfigurationService()
        {
            _topics.Add(KafkaTopic.RetentionCheckScheduled);
            _topics.Add(KafkaTopic.ReportScheduled);
        }


        public FacilityConfigurationService(IFacilityConfigurationRepo facilityConfigurationRepo, ILogger<FacilityConfigurationService> logger, IKafkaProducerFactory<string, object> kafkaProducerFactory, CreateAuditEventCommand createAuditEventCommand, IOptions<MeasureApiConfig> measureApiConfig, HttpClient httpClient)
        {
            _facilityConfigurationRepo = facilityConfigurationRepo;
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
            _measureApiConfig = measureApiConfig ?? throw new ArgumentNullException(nameof(_measureApiConfig));
            _logger = logger;
            _httpClient = httpClient;
            _createAuditEventCommand = createAuditEventCommand;
        }

        public async Task<List<FacilityConfigModel>> GetAllFacilities(CancellationToken cancellationToken)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get All Facilities Query");

            return await _facilityConfigurationRepo.GetAsync(cancellationToken);
        }

        public async Task<List<FacilityConfigModel>> GetFacilitiesByFilters(string? facilityId, string? facilityName, CancellationToken cancellationToken)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Facilities By Filters Query");

            return await _facilityConfigurationRepo.SearchAsync(facilityId, facilityName, cancellationToken);
        }

        public async Task<FacilityConfigModel> GetFacilityById(Guid id, CancellationToken cancellationToken)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Facility By Id Query");
            return await _facilityConfigurationRepo.GetAsyncById(id, cancellationToken);
        }

        public async Task<FacilityConfigModel> GetFacilityByFacilityId(string facilityId, CancellationToken cancellationToken)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Facility By Facility Id Query");

            return await _facilityConfigurationRepo.GetAsyncByFacilityId(facilityId, cancellationToken);
        }

        public async Task CreateFacility(FacilityConfigModel newFacility, CancellationToken cancellationToken)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Create Facility Configuration");

            //add id to current activity
            var currentActivity = Activity.Current;
            currentActivity?.AddTag("facility.id", newFacility.FacilityId);

            using (ServiceActivitySource.Instance.StartActivity("Validate the Facility Configuration"))
            {
                ValidateFacility(newFacility);

                var facility = await GetFacilityByFacilityId(newFacility.FacilityId, cancellationToken);

                // validates facility does not exist
                if (facility is not null)
                {
                    _logger.LogError($"Facility {newFacility.FacilityId} already exists");

                    throw new ApplicationException($"Facility {newFacility.FacilityId} already exists");
                }

                ValidateSchedules(newFacility);
            }

            try
            {

                using (ServiceActivitySource.Instance.StartActivity("Create the Facility Configuration Command"))
                {
                    newFacility.MRPCreatedDate = DateTime.UtcNow;
                    newFacility.Id = Guid.NewGuid();
                    await _facilityConfigurationRepo.AddAsync(newFacility, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex, new TagList {
                    { "service.name",TenantConstants.ServiceName },
                    { "facility", newFacility.FacilityId },
                    { "action", AuditEventType.Create },
                    { "resource", newFacility }
                });
                throw new ApplicationException($"Facility {newFacility.FacilityId} failed to create. " + ex.Message);
            }

            // send audit event
            AuditEventMessage auditMessageEvent = Helper.CreateFacilityAuditEvent(newFacility);
            _ = Task.Run(() => _createAuditEventCommand.Execute(newFacility.FacilityId, auditMessageEvent, cancellationToken));

        }

        public async Task<string> UpdateFacility(Guid id, FacilityConfigModel newFacility, CancellationToken cancellationToken = default)
        {
            FacilityConfigModel existingFacility;

            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Update Facility Configuration");

            //add id to current activity
            var currentActivity = Activity.Current;

            currentActivity?.AddTag("facility.id", newFacility.FacilityId);

            using (ServiceActivitySource.Instance.StartActivity("Validate the Facility Configuration"))
            {
                ValidateFacility(newFacility);

                existingFacility = GetFacilityById(id, cancellationToken).Result;

                if (existingFacility is null)
                {
                    _logger.LogError($"Facility with Id: {id} Not Found");

                    throw new ApplicationException($"Facility with Id: {id} Not Found");
                }

                FacilityConfigModel foundFacility = GetFacilityByFacilityId(newFacility.FacilityId, cancellationToken).Result;

                if (foundFacility != null && foundFacility.Id != id)
                {
                    _logger.LogError($"Facility {newFacility.FacilityId} already exists");

                    throw new ApplicationException($"Facility {newFacility.FacilityId} already exists");
                }

                ValidateSchedules(newFacility);
            }
            // audit update facility event
            AuditEventMessage auditMessageEvent = Helper.UpdateFacilityAuditEvent(newFacility, existingFacility);

            try
            {
                using (ServiceActivitySource.Instance.StartActivity("Update the Facility Command"))
                {
                    existingFacility.FacilityId = newFacility.FacilityId;
                    existingFacility.FacilityName = newFacility.FacilityName;
                    existingFacility.ScheduledTasks = newFacility.ScheduledTasks;
                    existingFacility.MonthlyReportingPlans = newFacility.MonthlyReportingPlans;

                    await _facilityConfigurationRepo.UpdateAsync(existingFacility, cancellationToken);

                }
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex, new TagList {
                    { "service.name",TenantConstants.ServiceName },
                    { "facility", newFacility.FacilityId },
                    { "action", AuditEventType.Update },
                    { "resource", newFacility }
                });
                throw new ApplicationException($"Facility {newFacility.FacilityId} failed to create. " + ex.Message);
            }

            // audit update facility event          
            _ = Task.Run(() => _createAuditEventCommand.Execute(newFacility.FacilityId, auditMessageEvent, cancellationToken));
            return id.ToString();
        }

        public async Task<string> RemoveFacility(string facilityId, CancellationToken cancellationToken)
        {
            FacilityConfigModel existingFacility;

            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Delete Facility Configuration");

            var currentActivity = Activity.Current;

            currentActivity?.AddTag("facility.id", facilityId);

            // validate facility exists
            using (ServiceActivitySource.Instance.StartActivity("Validate the Facility Configuration"))
            {
                existingFacility = GetFacilityByFacilityId(facilityId, cancellationToken).Result;

                if (existingFacility is null)
                {
                    _logger.LogError($"Facility with Id: {facilityId} Not Found");
                    throw new ApplicationException($"Facility with Id: {facilityId} Not Found");
                }
            }

            try
            {
                using (ServiceActivitySource.Instance.StartActivity("Delete the Facility Configuration Command"))
                {
                    await _facilityConfigurationRepo.RemoveAsync(existingFacility.Id, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex, new TagList {
                    { "service.name",TenantConstants.ServiceName },
                    { "facility", facilityId },
                    { "action", AuditEventType.Delete },
                    { "resource", existingFacility }
                });
                throw new ApplicationException($"Facility {facilityId} failed to delete. " + ex.Message);
            }

            // audit delete facility event
            AuditEventMessage auditMessageEvent = Helper.DeleteFacilityAuditEvent(existingFacility);
            _ = Task.Run(() => _createAuditEventCommand.Execute(existingFacility.FacilityId, auditMessageEvent, cancellationToken));
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
            if (facility.ScheduledTasks == null) return;

            foreach (ScheduledTaskModel scheduledTask in facility.ScheduledTasks)
            {
                // validate topic
                if (scheduledTask.KafkaTopic != null && !Helper.ValidateTopic(scheduledTask.KafkaTopic, _topics))
                {
                    throw new ApplicationException($"kafka topic {scheduledTask.KafkaTopic} is Invalid. Valid values are {string.Join(" and ", _topics.ToList())}");
                }

                // validate scheduleTrigger
                foreach (ScheduledTaskModel.ReportTypeSchedule reportTypeSchedule in scheduledTask.ReportTypeSchedules)
                {
                    if (reportTypeSchedule.ScheduledTriggers != null)
                    {
                        foreach (string trigger in reportTypeSchedule.ScheduledTriggers)
                        {
                            if (!Helper.IsValidSchedule(trigger))
                            {
                                _logger.LogError($"ScheduledTrigger {trigger} for facility {facility.FacilityId} and kafka topic {scheduledTask.KafkaTopic} is not valid chron expression");
                                throw new ApplicationException($"ScheduledTrigger {trigger} for facility {facility.FacilityId} and kafka topic {scheduledTask.KafkaTopic} is not valid chron expression");
                            }
                        }
                    }
                }
            }
            // validate topic is unique within Facility

            IEnumerable<string?> duplicates = facility.ScheduledTasks.GroupBy(i => i.KafkaTopic).Where(g => g.Count() > 1).Select(g => g.Key);

            string duplicatedTopics = string.Join(" ", duplicates.ToList());

            if (!duplicatedTopics.Equals(""))
            {
                _logger.LogError($"The following topics {duplicatedTopics} are duplicated for facility {facility.FacilityId} ");
                throw new ApplicationException($"The following topics {duplicatedTopics} are duplicated for facility {facility.FacilityId} ");
            }
            // validate report Type within Topic
            StringBuilder schedulingErrors = new StringBuilder();
            foreach (ScheduledTaskModel facilityScheduledTask in facility.ScheduledTasks)
            {

                IEnumerable<string?> duplicatedReportTypes = facilityScheduledTask.ReportTypeSchedules.GroupBy(i => i.ReportType).Where(g => g.Count() > 1).Select(g => g.Key);

                string duplicatedReportTypesString = string.Join(" ", duplicatedReportTypes.ToList());

                if (!duplicatedReportTypesString.Equals(""))
                {
                    _logger.LogError($"The following ReportTypes {duplicatedReportTypesString} are duplicated for facility {facility.FacilityId} and KafakaTopic {facilityScheduledTask.KafkaTopic}");
                    throw new ApplicationException($"The following ReportTypes {duplicatedReportTypesString} are duplicated for facility {facility.FacilityId} and KafakaTopic {facilityScheduledTask.KafkaTopic}");
                }

                int i = 1;
                foreach (ReportTypeSchedule reportTypeSchedule in facilityScheduledTask.ReportTypeSchedules)
                {
                    if (reportTypeSchedule.ReportType == null)
                    {
                        throw new ApplicationException($"ReportType under KafkaTopic {facilityScheduledTask.KafkaTopic} must be specified.");
                    }

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

                    string requestUrl = _measureApiConfig.Value.MeasureServiceApiUrl + $"/{reportTypeSchedule.ReportType}";

                    if (String.IsNullOrEmpty(_measureApiConfig.Value.MeasureServiceApiUrl))
                    {
                        throw new ApplicationException($"MeasureEval service configuration from \"MeasureServiceRegistry.MeasureServiceApiUrl\" is missing");
                    }

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
                        _logger.LogError($"The following Trigger {duplicatedTriggersString} are duplicated for facility {facility.FacilityId} and KafakaTopic {facilityScheduledTask.KafkaTopic} and reportType {reportTypeSchedule.ReportType}");
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
