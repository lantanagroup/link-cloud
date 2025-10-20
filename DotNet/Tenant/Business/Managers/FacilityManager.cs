using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Tenant.Business.Queries;
using LantanaGroup.Link.Tenant.Commands;
using LantanaGroup.Link.Tenant.Config;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Services;
using LantanaGroup.Link.Tenant.Utils;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using static LantanaGroup.Link.Shared.Application.Extensions.Security.BackendAuthenticationServiceExtension;

namespace LantanaGroup.Link.Tenant.Business.Managers
{
    public interface IFacilityManager
    {
        Task CreateAsync(Facility newFacility, CancellationToken cancellationToken = default);
        Task<string> UpdateAsync(System.Guid id, Facility newFacility, CancellationToken cancellationToken = default);
        Task<string> DeleteAsync(string facilityId, CancellationToken cancellationToken = default);
        Task MeasureDefinitionExists(string reportType);
    }

    public class FacilityManager : IFacilityManager
    {
        private readonly ILogger<FacilityManager> _logger;
        private readonly HttpClient _httpClient;
        private readonly IEntityRepository<Facility> _repository;
        private readonly IFacilityQueries _queries;
        private readonly CreateAuditEventCommand _createAuditEventCommand;
        private readonly IOptions<ServiceRegistry> _serviceRegistry;
        private readonly IOptions<MeasureConfig> _measureConfig;
        private readonly IOptions<LinkTokenServiceSettings> _linkTokenServiceConfig;
        private readonly ICreateSystemToken _createSystemToken;
        private readonly IOptions<LinkBearerServiceOptions> _linkBearerServiceOptions;

        public FacilityManager(
            ILogger<FacilityManager> logger,
            HttpClient httpClient,
            IEntityRepository<Facility> repository,
            IFacilityQueries queries,
            CreateAuditEventCommand createAuditEventCommand,
            IOptions<ServiceRegistry> serviceRegistry,
            IOptions<MeasureConfig> measureConfig,
            IOptions<LinkTokenServiceSettings> linkTokenServiceConfig,
            ICreateSystemToken createSystemToken,
            IOptions<LinkBearerServiceOptions> linkBearerServiceOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
            _createAuditEventCommand = createAuditEventCommand ?? throw new ArgumentNullException(nameof(createAuditEventCommand));
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
            _measureConfig = measureConfig ?? throw new ArgumentNullException(nameof(measureConfig));
            _linkTokenServiceConfig = linkTokenServiceConfig ?? throw new ArgumentNullException(nameof(linkTokenServiceConfig));
            _createSystemToken = createSystemToken ?? throw new ArgumentNullException(nameof(createSystemToken));
            _linkBearerServiceOptions = linkBearerServiceOptions ?? throw new ArgumentNullException(nameof(linkBearerServiceOptions));
        }

        public async Task CreateAsync(Facility newFacility, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Create Facility Configuration");

            //add id to current activity
            var currentActivity = Activity.Current;
            currentActivity?.AddTag("facility.id", newFacility.FacilityId);

            using (ServiceActivitySource.Instance.StartActivity("Validate the Facility Configuration"))
            {
                ValidateFacility(newFacility);

                var facility = await _repository.FirstOrDefaultAsync(f => f.FacilityId == newFacility.FacilityId, cancellationToken);

                // validates facility 
                if (facility is not null)
                {
                    _logger.LogError("Facility {FacilityId} already exists", HtmlInputSanitizer.Sanitize(newFacility.FacilityId));

                    throw new ApplicationException($"Facility {newFacility.FacilityId} already exists");
                }

                await ValidateSchedules(newFacility);
            }

            try
            {
                using (ServiceActivitySource.Instance.StartActivity("Create the Facility Configuration Command"))
                {
                    newFacility.CreateDate = DateTime.UtcNow;
                    if(newFacility.Id == default) 
                        newFacility.Id = Guid.NewGuid();

                    await _repository.AddAsync(newFacility, cancellationToken);
                    await _repository.SaveChangesAsync(cancellationToken);
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

        public async Task<string> UpdateAsync(Guid id, Facility newFacility, CancellationToken cancellationToken = default)
        {
            Facility? existingFacility;

            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Update Facility Configuration");

            //add id to current activity
            var currentActivity = Activity.Current;

            currentActivity?.AddTag("facility.id", newFacility.FacilityId);

            using (ServiceActivitySource.Instance.StartActivity("Validate the Facility Configuration"))
            {
                existingFacility = await _repository.GetAsync(id, cancellationToken);

                ValidateFacility(newFacility);

                var foundFacility = await _repository.FirstOrDefaultAsync(f => f.FacilityId == newFacility.FacilityId, cancellationToken);

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
                        if (existingFacility.FacilityId != newFacility.FacilityId)
                        {
                            throw new ApplicationException("Cannot change the FacilityId of a facility.");
                        }
                        existingFacility.FacilityName = newFacility.FacilityName;
                        existingFacility.TimeZone = newFacility.TimeZone;
                        existingFacility.ScheduledReports.Daily = newFacility.ScheduledReports.Daily;
                        existingFacility.ScheduledReports.Weekly = newFacility.ScheduledReports.Weekly;
                        existingFacility.ScheduledReports.Monthly = newFacility.ScheduledReports.Monthly;
                        _repository.Update(existingFacility);
                        await _repository.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        throw new ApplicationException($"Facility with ID {id} not found.");
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
                throw new ApplicationException($"Facility {newFacility.FacilityId} failed to update. " + ex.Message);
            }

            // audit update facility event          
            _ = Task.Run(() => _createAuditEventCommand.Execute(newFacility.FacilityId, auditMessageEvent, cancellationToken));
            return id.ToString();
        }

        public async Task<string> DeleteAsync(string facilityId, CancellationToken cancellationToken = default)
        {
            Facility? existingFacility;

            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Delete Facility Configuration");

            var currentActivity = Activity.Current;

            currentActivity?.AddTag("facility.id", facilityId);

            // validate facility exists
            using (ServiceActivitySource.Instance.StartActivity("Validate the Facility Configuration"))
            {
                existingFacility = await _repository.FirstOrDefaultAsync(f => f.FacilityId == facilityId, cancellationToken);

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
                    _repository.Remove(existingFacility);
                    await _repository.SaveChangesAsync(cancellationToken);
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

        public async Task MeasureDefinitionExists(string reportType)
        {
            if (_measureConfig.Value.CheckIfMeasureExists)
            {
                if (String.IsNullOrEmpty(_serviceRegistry.Value.MeasureServiceUrl))
                    throw new ApplicationException($"MeasureEval service configuration from \"ServiceRegistry.MeasureServiceUrl\" is missing");


                var requestUrl = new Uri(new Uri(_serviceRegistry.Value.MeasureServiceUrl), $"api/measure-definition/{HtmlInputSanitizer.SanitizeAndRemove(reportType)}");

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

        private void ValidateFacility(Facility facility)
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
                    _logger.LogError("Incorrect Timezone format: " + facility.TimeZone + "(Time zones should be in IANA format for example: America/Chicago)");
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

        private async Task ValidateSchedules(Facility facility)
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
        }

        private static HashSet<string> FindDuplicates(List<string> list)
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