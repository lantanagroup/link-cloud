using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
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
using System.Net.Http.Headers;
using System.Text;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using System.Linq.Expressions;
using Confluent.Kafka;
using static LantanaGroup.Link.Shared.Application.Extensions.Security.BackendAuthenticationServiceExtension;
using LantanaGroup.Link.Tenant.Interfaces;
using LantanaGroup.Link.Shared.Application.Services.Security;


namespace LantanaGroup.Link.Tenant.Services
{
    public class FacilityConfigurationService : IFacilityConfigurationService
    {

        private readonly ILogger<IFacilityConfigurationService> _logger;
        private readonly HttpClient _httpClient;
        private static   List<KafkaTopic> _topics = new List<KafkaTopic>();
        private readonly IOptions<ServiceRegistry> _serviceRegistry;
        private readonly IFacilityConfigurationRepo _facilityConfigurationRepo;
        private readonly CreateAuditEventCommand _createAuditEventCommand;
        private readonly IOptions<MeasureConfig> _measureConfig;
        private readonly IOptions<LinkTokenServiceSettings> _linkTokenServiceConfig;
        private readonly ICreateSystemToken _createSystemToken;
        private readonly IOptions<LinkBearerServiceOptions> _linkBearerServiceOptions;

        static FacilityConfigurationService()
        {
            _topics.Add(KafkaTopic.RetentionCheckScheduled);
            _topics.Add(KafkaTopic.ReportScheduled);
        }

        public FacilityConfigurationService(IFacilityConfigurationRepo facilityConfigurationRepo, ILogger<FacilityConfigurationService> logger, CreateAuditEventCommand createAuditEventCommand, IOptions<ServiceRegistry> serviceRegistry, IOptions<MeasureConfig> measureConfig, HttpClient httpClient, IOptions<LinkTokenServiceSettings> linkTokenServiceConfig, ICreateSystemToken createSystemToken, IOptions<LinkBearerServiceOptions> linkBearerServiceOptions)
        {
            _facilityConfigurationRepo = facilityConfigurationRepo;
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
            _measureConfig = measureConfig ?? throw new ArgumentNullException(nameof(measureConfig));
            _logger = logger;
            _httpClient = httpClient;
            _createAuditEventCommand = createAuditEventCommand;
            _linkTokenServiceConfig = linkTokenServiceConfig ?? throw new ArgumentNullException(nameof(linkTokenServiceConfig));
            _createSystemToken = createSystemToken ?? throw new ArgumentNullException(nameof(createSystemToken));
            _linkBearerServiceOptions = linkBearerServiceOptions ?? throw new ArgumentNullException(nameof(linkBearerServiceOptions));
        }

        public async Task<List<FacilityConfigModel>> GetAllFacilities(CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Facilities By Filters Query");

            return await _facilityConfigurationRepo.GetAllAsync();

        }

        public async Task<PagedConfigModel<FacilityConfigModel>> GetFacilities(string? facilityId, string? facilityName, string? sortBy, SortOrder? sortOrder, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Facilities By Filters Query");
            PagedConfigModel<FacilityConfigModel> pagedNotificationConfigurations;


            if (!string.IsNullOrEmpty(facilityId) || !string.IsNullOrEmpty(facilityName))
            {
                (List<FacilityConfigModel> facilities, PaginationMetadata metadata) = await _facilityConfigurationRepo.SearchAsync((x => x.FacilityId == facilityId && facilityId != null || x.FacilityName == facilityName && facilityName != null), sortBy, sortOrder, pageSize, pageNumber, cancellationToken);
                pagedNotificationConfigurations = new PagedConfigModel<FacilityConfigModel>(facilities, metadata);
            }
            else
            {
                if (sortBy == null)
                {
                    sortBy = "FacilityId";
                }
                if (sortOrder == null)
                {
                    sortOrder = SortOrder.Ascending;
                }
                (List<FacilityConfigModel> facilities, PaginationMetadata metadata) = await _facilityConfigurationRepo.SearchAsync(null, sortBy, sortOrder, pageSize, pageNumber, cancellationToken);
                pagedNotificationConfigurations = new PagedConfigModel<FacilityConfigModel>(facilities, metadata);
            }
            

            return pagedNotificationConfigurations;
        }

        public async Task<FacilityConfigModel> GetFacilityById(string id, CancellationToken cancellationToken)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Facility By Id Query");
            return await _facilityConfigurationRepo.GetAsync(id, cancellationToken);
        }

        public async Task<FacilityConfigModel> GetFacilityByFacilityId(string facilityId, CancellationToken cancellationToken)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Facility By Facility Id Query");

            return await _facilityConfigurationRepo.FirstOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);
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

                // validates facility 
                if (facility is not null)
                {
                    _logger.LogError($"Facility {HtmlInputSanitizer.Sanitize(newFacility.FacilityId)} already exists");

                    throw new ApplicationException($"Facility {newFacility.FacilityId} already exists");
                }

                await ValidateSchedules(newFacility);
            }

            try
            {

                using (ServiceActivitySource.Instance.StartActivity("Create the Facility Configuration Command"))
                {
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

        public async Task<string> UpdateFacility(String id, FacilityConfigModel newFacility, CancellationToken cancellationToken = default)
        {
            FacilityConfigModel existingFacility;

            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Update Facility Configuration");

            //add id to current activity
            var currentActivity = Activity.Current;

            currentActivity?.AddTag("facility.id", newFacility.FacilityId);

            using (ServiceActivitySource.Instance.StartActivity("Validate the Facility Configuration"))
            {
                existingFacility = GetFacilityById(id, cancellationToken).Result;

                ValidateFacility(newFacility);

                FacilityConfigModel foundFacility = GetFacilityByFacilityId(newFacility.FacilityId, cancellationToken).Result;

                if (foundFacility != null && foundFacility.Id != id)
                {
                    _logger.LogError($"Facility {HtmlInputSanitizer.Sanitize(newFacility.FacilityId)} already exists");

                    throw new ApplicationException($"Facility {newFacility.FacilityId} already exists under another ID: {foundFacility.Id}");
                }

                await ValidateSchedules(newFacility);
            }
                
            // audit update facility event
            AuditEventMessage auditMessageEvent = Helper.UpdateFacilityAuditEvent(newFacility, existingFacility);

            try
            {
                using (ServiceActivitySource.Instance.StartActivity("Update the Facility Command"))
                {
                    if (existingFacility is not null)
                    {
                        existingFacility.FacilityId = newFacility.FacilityId;
                        existingFacility.FacilityName = newFacility.FacilityName;
                        existingFacility.ScheduledReports = newFacility.ScheduledReports;
                        existingFacility.TimeZone = newFacility.TimeZone;
                        await _facilityConfigurationRepo.UpdateAsync(existingFacility, cancellationToken);
                    }
                    else
                    {
                        await _facilityConfigurationRepo.AddAsync(newFacility, cancellationToken);
                    }
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
                    _logger.LogError($"Facility with Id: {HtmlInputSanitizer.Sanitize(facilityId)} Not Found");
                    throw new ApplicationException($"Facility with Id: {facilityId} Not Found");
                }
            }
           
            try
            {
                using (ServiceActivitySource.Instance.StartActivity("Delete the Facility Configuration Command"))
                {
                    await _facilityConfigurationRepo.DeleteAsync(existingFacility.Id, cancellationToken);
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
            // validate timezones
            try
            {
                // Try to find the time zone based on the ID stored in the facility object
                TimeZoneInfo timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(facility.TimeZone);

                _logger.LogInformation($"Time zone found: {timeZoneInfo.StandardName}");

                // verify the id of the time zone is IANA format
                if (!timeZoneInfo.HasIanaId)
                {
                    _logger.LogError("Incorrect Timezone format: " + facility.TimeZone +  "(Time zones should be in IANA format for example: America/Chicago)");
                    throw new ApplicationException("Incorrect Timezone format: " + facility.TimeZone + " (Time zones should be in IANA format for example: America/Chicago)");
                }
            }
            catch (TimeZoneNotFoundException)
            {
                _logger.LogError($"The time zone ID '{facility.TimeZone}' was not found on this system.");
                throw new ApplicationException("Timezone Not Found: " + facility.TimeZone);
            }
            catch (InvalidTimeZoneException)
            {
                _logger.LogError("Invalid Timezone: " + facility.TimeZone);
                throw new ApplicationException("Invalid Timezone: " + facility.TimeZone);
            }


        }

        private async Task ValidateSchedules(FacilityConfigModel facility)
        {
            List<string> reportTypes = new List<string>();
            reportTypes.AddRange(facility.ScheduledReports.Monthly);
            reportTypes.AddRange(facility.ScheduledReports.Daily);
            reportTypes.AddRange(facility.ScheduledReports.Weekly);

            HashSet<string> duplicates = FindDuplicates(reportTypes);
            if (duplicates.Count > 0)
            {
                _logger.LogError("Duplicate entries found: " + string.Join(", ", duplicates));
                throw new ApplicationException("Duplicate entries found: " + string.Join(", ", duplicates));
            }
            // validate report types exist
            foreach (var reportType in reportTypes)
            {
                await MeasureDefinitionExists(reportType);
            }

            return;
        }

        private async Task MeasureDefinitionExists(String reportType)
        {
            if (_measureConfig.Value.CheckIfMeasureExists)
            {
                if (String.IsNullOrEmpty(_serviceRegistry.Value.MeasureServiceUrl))
                    throw new ApplicationException($"MeasureEval service configuration from \"ServiceRegistry.MeasureServiceUrl\" is missing");

                string requestUrl = _serviceRegistry.Value.MeasureServiceUrl + $"/api/measure-definition/{reportType}";

                //get link token
                if (!_linkBearerServiceOptions.Value.AllowAnonymous)
                {
                    //TODO: add method to get key that includes looking at redis for future use case
                    if (_linkTokenServiceConfig.Value.SigningKey is null) throw new Exception("Link Token Service Signing Key is missing.");

                    var token = await _createSystemToken.ExecuteAsync(_linkTokenServiceConfig.Value.SigningKey, 2);
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                var response = await _httpClient.GetAsync(requestUrl, CancellationToken.None);

                // check respone status code
                if (!response.IsSuccessStatusCode)
                {
                    throw new ApplicationException($"Report Type {reportType} is not setup in MeasureEval service.");
                }
            }
        }

        static HashSet<string> FindDuplicates(List<string> list)
        {
            HashSet<string> uniqueItems = new HashSet<string>();
            HashSet<string> duplicates = new HashSet<string>();

            foreach (string item in list)
            {
                if (!uniqueItems.Add(item))
                {
                    duplicates.Add(item);
                }
            }
            return duplicates;
        }

    }
}
