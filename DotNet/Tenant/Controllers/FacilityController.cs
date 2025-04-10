using AutoMapper;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Interfaces;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Services;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Quartz;
using System.Diagnostics;
using System.Net;

namespace LantanaGroup.Link.Tenant.Controllers
{
    [Route("api/[controller]")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class FacilityController : ControllerBase
    {

        private readonly IFacilityConfigurationService _facilityConfigurationService;
try
{
    _logger.LogInformation("Beginning facility deletion process for facility ID: {FacilityId}", facilityId);
    var deletionResults = new Dictionary

&lt;

string, string

&gt;

();

    // Loop through the registered services (skipping the Tenant service) to call DELETE endpoints
    foreach (var service in _serviceRegistry.Services)
    {
        if (service.Key == "Tenant") continue;
    
        string deleteEndpoint = "";
        switch (service.Key)
        {
            case "Census":
                deleteEndpoint = $"/api/configuration/{facilityId}";
                break;
            case "Normalization":
                deleteEndpoint = $"/api/normalization/tenant/{facilityId}";
                break;
            case "QueryDispatch":
                deleteEndpoint = $"/api/query-dispatch/configuration/{facilityId}";
                break;
            case "DataAcquisition":
                deleteEndpoint = $"/api/data-acquisition/facility/{facilityId}";
                break;
            case "Report":
                deleteEndpoint = $"/api/report/configuration/{facilityId}";
                break;
            case "Measure":
                deleteEndpoint = $"/api/measure/configuration/{facilityId}";
                break;
            case "Submission":
                deleteEndpoint = $"/api/submission/configuration/{facilityId}";
                break;
            case "Notification":
                deleteEndpoint = $"/api/notification/configuration/{facilityId}";
                break;
            default:
                _logger.LogWarning("No deletion endpoint defined for service: {ServiceKey}", service.Key);
                deletionResults[service.Key] = "Skipped - No deletion endpoint defined";
                continue;
        }
        
        // Create an HttpClient and call the DELETE endpoint
        try
        {
            var client = _httpClient.CreateClient();
            var url = $"{service.Value.BaseUrl}{deleteEndpoint}";
            _logger.LogInformation("Calling delete endpoint for service {ServiceKey}: {Url}", service.Key, url);
            var response = await client.DeleteAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully deleted facility data from {ServiceKey} service", service.Key);
                deletionResults[service.Key] = "Success";
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to delete facility data from {ServiceKey} service. Status: {StatusCode}, Error: {Error}",
                    service.Key, response.StatusCode, errorContent);
                deletionResults[service.Key] = $"Failed - {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting facility data from {ServiceKey} service", service.Key);
            deletionResults[service.Key] = $"Error - {ex.Message}";
        }
    }
    // After processing other services, delete the facility from the Tenant service
    _logger.LogInformation("Deleting facility from Tenant service");
    await _facilityConfigurationService.RemoveFacility(facilityId, cancellationToken);
    deletionResults["Tenant"] = "Success";
    // Log overall deletion results and complete the process
    _logger.LogInformation("Facility deletion completed for {FacilityId}. Results: {@DeletionResults}", facilityId, deletionResults);
    return NoContent();
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error in facility deletion process for {FacilityId}", facilityId);
    return StatusCode(500, new
    {
        error = "Failed to complete facility deletion process",
        message = ex.Message,
        deletionResults = deletionResults
    });
}

        /// <summary>
        /// Find a facility config by Id
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FacilityConfigDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet("{facilityId}")]
        public async Task<ActionResult<FacilityConfigDto>> LookupFacilityById(string facilityId, CancellationToken cancellationToken)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Facility By Facility Id");

            var facility = await _facilityConfigurationService.GetFacilityByFacilityId(facilityId, cancellationToken);

            if (facility == null)
            {
                return NotFound($"Facility with Id: {facilityId} Not Found");
            }

            FacilityConfigDto? dest = null;

            using (ServiceActivitySource.Instance.StartActivity("Map Result"))
            {
                dest = _mapperModelToDto.Map<FacilityConfigModel, FacilityConfigDto>(facility);
            }

            return Ok(dest);
        }


        /// <summary>
        /// Update a facility config.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="updatedFacility"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(FacilityConfigDto))]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateFacility(string id, FacilityConfigDto updatedFacility, CancellationToken cancellationToken)
        {
            FacilityConfigModel dest = _mapperDtoToModel.Map<FacilityConfigDto, FacilityConfigModel>(updatedFacility);

            // validate id and updatedFacility.id match
            if (id.ToString() != updatedFacility.Id)
            {
                return BadRequest($" {id} in the url and the {updatedFacility.Id} in the payload mismatch");
            }

             FacilityConfigModel oldFacility = await _facilityConfigurationService.GetFacilityById(id, cancellationToken);

             FacilityConfigModel clonedFacility = oldFacility?.ShallowCopy();

            try
            {
                await _facilityConfigurationService.UpdateFacility(id, dest, cancellationToken);
            }
            catch (ApplicationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception Encountered in FacilityController.UpdateFacility");
                return Problem("An error occurred while updating the facility", null, 500);
            }

            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

            // if clonedFacility is not null, then update the jobs, else add new jobs

            if (clonedFacility != null)
            {
                using (ServiceActivitySource.Instance.StartActivity("Update Jobs for Facility"))
                {
                    await ScheduleService.UpdateJobsForFacility(dest, clonedFacility, scheduler);
                }
            }
            else
            {
                using (ServiceActivitySource.Instance.StartActivity("Create Jobs for Facility"))
                {
                    await ScheduleService.AddJobsForFacility(dest, scheduler);
                }
            }

            if (oldFacility == null)
            {
                return CreatedAtAction(nameof(StoreFacility), new { id = dest.Id }, dest);
            }

            return NoContent();
        }

        /// <summary>
        /// Delete a facility by Id.
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpDelete("{facilityId}")]
        public async Task

        <IActionResult> DeleteFacility(string facilityId, CancellationToken cancellationToken)
        {
            var deletionResults = new Dictionary<string, string>();
            try
            {
                _logger.LogInformation("Beginning facility deletion process for facility ID: {FacilityId}", facilityId);
                
                // Loop through each service in the registry (skipping the Tenant service)
                foreach (var service in _serviceRegistry.Services)
                {
                    if (service.Key == "Tenant") continue;
                    
                    string deleteEndpoint = "";
                    switch (service.Key)
                    {
                        case "Census":
                            deleteEndpoint = $"/api/configuration/{facilityId}";
                            break;
                        case "Normalization":
                            deleteEndpoint = $"/api/normalization/tenant/{facilityId}";
                            break;
                        case "QueryDispatch":
                            deleteEndpoint = $"/api/query-dispatch/configuration/{facilityId}";
                            break;
                        case "DataAcquisition":
                            deleteEndpoint = $"/api/data-acquisition/facility/{facilityId}";
                            break;
                        case "Report":
                            deleteEndpoint = $"/api/report/configuration/{facilityId}";
                            break;
                        case "Measure":
                            deleteEndpoint = $"/api/measure/configuration/{facilityId}";
                            break;
                        case "Submission":
                            deleteEndpoint = $"/api/submission/configuration/{facilityId}";
                            break;
                        case "Notification":
                            deleteEndpoint = $"/api/notification/configuration/{facilityId}";
                            break;
                        default:
                            _logger.LogWarning("No deletion endpoint defined for service: {ServiceKey}", service.Key);
                            deletionResults[service.Key] = "Skipped - No deletion endpoint defined";
                            continue;
                    }
                    
                    // For each service call, create an HttpClient and issue a DELETE request
                    try
                    {
                        var client = _httpClient.CreateClient();
                        var url = $"{service.Value.BaseUrl}{deleteEndpoint}";
                        _logger.LogInformation("Calling delete endpoint for service {ServiceKey}: {Url}", service.Key, url);
                        
                        var response = await client.DeleteAsync(url, cancellationToken);
                        if (response.IsSuccessStatusCode)
                        {
                            _logger.LogInformation("Successfully deleted facility data from {ServiceKey} service", service.Key);
                            deletionResults[service.Key] = "Success";
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                            _logger.LogWarning("Failed to delete facility data from {ServiceKey} service. Status: {StatusCode}, Error: {Error}",
                                service.Key, response.StatusCode, errorContent);
                            deletionResults[service.Key] = $"Failed - {response.StatusCode}";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deleting facility data from {ServiceKey} service", service.Key);
                        deletionResults[service.Key] = $"Error - {ex.Message}";
                    }
                    // End of service loop iteration.
                }
                
                // After processing all services, delete the facility from the Tenant service.
                _logger.LogInformation("Deleting facility from Tenant service");
                await _facilityConfigurationService.RemoveFacility(facilityId, cancellationToken);
                deletionResults["Tenant"] = "Success";
                
                // Log overall deletion results and return NoContent to indicate successful deletion
                _logger.LogInformation("Facility deletion completed for {FacilityId}. Results: {@DeletionResults}",
                    facilityId, deletionResults);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in facility deletion process for {FacilityId}", facilityId);
                return StatusCode(500, new 
                { 
                    error = "Failed to complete facility deletion process", 
                    message = ex.Message,
                    deletionResults = deletionResults
                });
            }
        }

                    {
                        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogWarning("Failed to delete facility data from Census service. Status: {StatusCode}, Error: {Error}", response.StatusCode, errorContent);
                        deletionResults["Census"] = $"Failed - {response.StatusCode}";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting facility data from Census service");
                    deletionResults["Census"] = $"Error - {ex.Message}";
                }
            }

            if (!string.IsNullOrEmpty(_serviceRegistry.NormalizationServiceApiUrl))
            {
                try
                {
                    string deleteUrl = $"{_serviceRegistry.NormalizationServiceApiUrl}/api/normalization/tenant/{facilityId}";
                    _logger.LogInformation("Calling Normalization service delete endpoint: {DeleteUrl}", deleteUrl);
                    var response = await httpClient.DeleteAsync(deleteUrl, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Successfully deleted facility data from Normalization service");
                        deletionResults["Normalization"] = "Success";
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogWarning("Failed to delete facility data from Normalization service. Status: {StatusCode}, Error: {Error}", response.StatusCode, errorContent);
                        deletionResults["Normalization"] = $"Failed - {response.StatusCode}";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting facility data from Normalization service");
                    deletionResults["Normalization"] = $"Error - {ex.Message}";
                }
            }

            if (!string.IsNullOrEmpty(_serviceRegistry.QueryDispatchServiceApiUrl))
            {
                try
                {
                    string deleteUrl = $"{_serviceRegistry.QueryDispatchServiceApiUrl}/api/query-dispatch/configuration/{facilityId}";
                    _logger.LogInformation("Calling QueryDispatch service delete endpoint: {DeleteUrl}", deleteUrl);
                    var response = await httpClient.DeleteAsync(deleteUrl, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Successfully deleted facility data from QueryDispatch service");
                        deletionResults["QueryDispatch"] = "Success";
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogWarning("Failed to delete facility data from QueryDispatch service. Status: {StatusCode}, Error: {Error}", response.StatusCode, errorContent);
                        deletionResults["QueryDispatch"] = $"Failed - {response.StatusCode}";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting facility data from QueryDispatch service");
                    deletionResults["QueryDispatch"] = $"Error - {ex.Message}";
                }
            }

            if (!string.IsNullOrEmpty(_serviceRegistry.DataAcquisitionServiceApiUrl))
            {
                try
                {
                    string deleteUrl = $"{_serviceRegistry.DataAcquisitionServiceApiUrl}/api/data-acquisition/facility/{facilityId}";
                    _logger.LogInformation("Calling DataAcquisition service delete endpoint: {DeleteUrl}", deleteUrl);
                    var response = await httpClient.DeleteAsync(deleteUrl, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Successfully deleted facility data from DataAcquisition service");
                        deletionResults["DataAcquisition"] = "Success";
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogWarning("Failed to delete facility data from DataAcquisition service. Status: {StatusCode}, Error: {Error}", response.StatusCode, errorContent);
                        deletionResults["DataAcquisition"] = $"Failed - {response.StatusCode}";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting facility data from DataAcquisition service");
                    deletionResults["DataAcquisition"] = $"Error - {ex.Message}";
                }
            }

            if (!string.IsNullOrEmpty(_serviceRegistry.ReportServiceApiUrl))
            {
                try
                {
                    string deleteUrl = $"{_serviceRegistry.ReportServiceApiUrl}/api/report/configuration/{facilityId}";
                    _logger.LogInformation("Calling Report service delete endpoint: {DeleteUrl}", deleteUrl);
                    var response = await httpClient.DeleteAsync(deleteUrl, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Successfully deleted facility data from Report service");
                        deletionResults["Report"] = "Success";
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogWarning("Failed to delete facility data from Report service. Status: {StatusCode}, Error: {Error}", response.StatusCode, errorContent);
                        deletionResults["Report"] = $"Failed - {response.StatusCode}";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting facility data from Report service");
                    deletionResults["Report"] = $"Error - {ex.Message}";
                }
            }

            if (!string.IsNullOrEmpty(_serviceRegistry.MeasureServiceApiUrl))
            {
                try
                {
                    string deleteUrl = $"{_serviceRegistry.MeasureServiceApiUrl}/api/measure/configuration/{facilityId}";
                    _logger.LogInformation("Calling Measure service delete endpoint: {DeleteUrl}", deleteUrl);
                    var response = await httpClient.DeleteAsync(deleteUrl, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Successfully deleted facility data from Measure service");
                        deletionResults["Measure"] = "Success";
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogWarning("Failed to delete facility data from Measure service. Status: {StatusCode}, Error: {Error}", response.StatusCode, errorContent);
                        deletionResults["Measure"] = $"Failed - {response.StatusCode}";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting facility data from Measure service");
                    deletionResults["Measure"] = $"Error - {ex.Message}";
                }
            }

            if (!string.IsNullOrEmpty(_serviceRegistry.SubmissionServiceApiUrl))
            {
                try
                {
                    string deleteUrl = $"{_serviceRegistry.SubmissionServiceApiUrl}/api/submission/configuration/{facilityId}";
                    _logger.LogInformation("Calling Submission service delete endpoint: {DeleteUrl}", deleteUrl);
                    var response = await httpClient.DeleteAsync(deleteUrl, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Successfully deleted facility data from Submission service");
                        deletionResults["Submission"] = "Success";
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogWarning("Failed to delete facility data from Submission service. Status: {StatusCode}, Error: {Error}", response.StatusCode, errorContent);
                        deletionResults["Submission"] = $"Failed - {response.StatusCode}";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting facility data from Submission service");
                    deletionResults["Submission"] = $"Error - {ex.Message}";
                }
            }

            if (!string.IsNullOrEmpty(_serviceRegistry.NotificationServiceApiUrl))
            {
                try
                {
                    string deleteUrl = $"{_serviceRegistry.NotificationServiceApiUrl}/api/notification/configuration/{facilityId}";
                    _logger.LogInformation("Calling Notification service delete endpoint: {DeleteUrl}", deleteUrl);
                    var response = await httpClient.DeleteAsync(deleteUrl, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Successfully deleted facility data from Notification service");
                        deletionResults["Notification"] = "Success";
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogWarning("Failed to delete facility data from Notification service. Status: {StatusCode}, Error: {Error}", response.StatusCode, errorContent);
                        deletionResults["Notification"] = $"Failed - {response.StatusCode}";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting facility data from Notification service");
                    deletionResults["Notification"] = $"Error - {ex.Message}";
                }
            }

            _logger.LogInformation("Deleting facility from Tenant service");
            await _facilityService.DeleteFacilityAsync(facilityId, cancellationToken);
            deletionResults["Tenant"] = "Success";

            _logger.LogInformation("Facility deletion completed for {FacilityId}. Results: {@DeletionResults}", facilityId, deletionResults);

            using (ServiceActivitySource.Instance.StartActivity("Delete Jobs for Facility"))
            {
                var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
                await ScheduleService.DeleteJobsForFacility(existingFacility.Id.ToString(), scheduler);
            }

            return NoContent();
        }

                        case "DataAcquisition":
                            deleteEndpoint = $"/api/data-acquisition/facility/{facilityId}";
                            break;
                        case "Report":
                            deleteEndpoint = $"/api/report/configuration/{facilityId}";
                            break;
                        case "Measure":
                            deleteEndpoint = $"/api/measure/configuration/{facilityId}";
                            break;
                        case "Submission":
                            deleteEndpoint = $"/api/submission/configuration/{facilityId}";
                            break;
                        case "Notification":
                            deleteEndpoint = $"/api/notification/configuration/{facilityId}";
                            break;
                        default:
                            _logger.LogWarning("No deletion endpoint defined for service: {ServiceKey}", service.Key);
                            deletionResults[service.Key] = "Skipped - No deletion endpoint defined";
                            continue;
                    }

                    try
                    {
                        var client = _httpClient.CreateClient();
                        var url = $"{service.Value.BaseUrl}{deleteEndpoint}";
                        _logger.LogInformation("Calling delete endpoint for service {ServiceKey}: {Url}", service.Key, url);
                        var response = await client.DeleteAsync(url, cancellationToken);
                        if (response.IsSuccessStatusCode)
                        {
                            _logger.LogInformation("Successfully deleted facility data from {ServiceKey} service", service.Key);
                            deletionResults[service.Key] = "Success";
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                            _logger.LogWarning("Failed to delete facility data from {ServiceKey} service. Status: {StatusCode}, Error: {Error}", service.Key, response.StatusCode, errorContent);
                            deletionResults[service.Key] = $"Failed - {response.StatusCode}";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deleting facility data from {ServiceKey} service", service.Key);
                        deletionResults[service.Key] = $"Error - {ex.Message}";
                    }
                }
                
                _logger.LogInformation("Deleting facility from Tenant service");
                await _facilityConfigurationService.RemoveFacility(facilityId, cancellationToken);
                deletionResults["Tenant"] = "Success";

                _logger.LogInformation("Facility deletion completed for {FacilityId}. Results: {@DeletionResults}", facilityId, deletionResults);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in facility deletion process for {FacilityId}", facilityId);
                return StatusCode(500, new
                {
                    error = "Failed to complete facility deletion process",
                    message = ex.Message,
                    deletionResults = deletionResults
                });
            }
        }

        /// <summary>
        /// Generat
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPost("AdHocReport")]
        public async Task<IActionResult> GenerateAdHocReport(string facilityId, AdHocReportRequest request)
        {
            if (string.IsNullOrEmpty(facilityId) || await _facilityConfigurationService.GetFacilityByFacilityId(facilityId, CancellationToken.None) == null)
            {
                return BadRequest("Facility does not exist.");
            }

            if (request.ReportTypes == null || request.ReportTypes.Count == 0)
            {
                return BadRequest("ReportTypes must be provided.");
            }

            if (request.StartDate == null || request.StartDate == DateTime.MinValue)
            {
                return BadRequest("StartDate must be provided.");
            }

            if (request.EndDate == null || request.EndDate == DateTime.MinValue)
            {
                return BadRequest("EndDate must be provided.");
            }

            if (request.EndDate <= request.StartDate)
            {
                return BadRequest("EndDate must be after StartDate.");
            }

            try
            {
                foreach (var rt in request.ReportTypes)
                {
                    //this will throw an ApplicationException if the Measure Definition does not exist.
                    await _facilityConfigurationService.MeasureDefinitionExists(rt);
                }

                var producerConfig = new ProducerConfig();

                using var producer = _adHocKafkaProducerFactory.CreateProducer(producerConfig);

                var startDate = new DateTime(
                    request.StartDate.Value.Year,
                    request.StartDate.Value.Month,
                    request.StartDate.Value.Day,
                    request.StartDate.Value.Hour,
                    request.StartDate.Value.Minute,
                    request.StartDate.Value.Second,
                    DateTimeKind.Utc
                );

               var endDate = new DateTime(
                    request.EndDate.Value.Year,
                    request.EndDate.Value.Month,
                    request.EndDate.Value.Day,
                    request.EndDate.Value.Hour,
                    request.EndDate.Value.Minute,
                    request.EndDate.Value.Second,
                    DateTimeKind.Utc
                );

                var message = new Message<string, GenerateReportValue>
                {
                    Key = facilityId,
                    Headers = new Headers(),
                    Value = new GenerateReportValue
                    {
                        StartDate = startDate,
                        EndDate = endDate,
                        ReportTypes = request.ReportTypes,
                        PatientIds = request.PatientIds,
                        BypassSubmission = request.BypassSubmission?? false
                    },
                };

                await producer.ProduceAsync(KafkaTopic.GenerateReportRequested.ToString(), message, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception encountered in FacilityController.GenerateAdHocReport");
                return Problem("An internal server error occurred.", statusCode: 500);
            }

            return Ok();
        }

        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPost("RegenerateReport")]
        public async Task<IActionResult> RegenerateReport(string facilityId, RegenerateReportRequest request)
        {
            if (string.IsNullOrEmpty(facilityId) || await _facilityConfigurationService.GetFacilityByFacilityId(facilityId, CancellationToken.None) == null)
            {
                return BadRequest("Facility does not exist.");
            }

            if (string.IsNullOrEmpty(request.ReportId))
            {
                return BadRequest("ReportId must be provided.");
            }

            try
            {
                var httpClient = _httpClient.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                string requestUrl = $"{_serviceRegistry.ReportServiceApiUrl.Trim('/')}/Report/Schedule?FacilityId={facilityId}&reportScheduleId={request.ReportId}";

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var response = await httpClient.GetAsync(requestUrl, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception(
                        $"Report Service Call unsuccessful: StatusCode: {response.StatusCode} | Response: {await response.Content.ReadAsStringAsync(CancellationToken.None)} | Query URL: {requestUrl}");
                }

                var reportScheduleSummary = (ReportScheduleSummaryModel?)await response.Content.ReadFromJsonAsync(typeof(ReportScheduleSummaryModel), CancellationToken.None);

                if (reportScheduleSummary == null)
                {
                    return Problem("No ReportSchedule found for the provided ReportScheduleId", statusCode: (int)HttpStatusCode.NotFound);
                }

                var producerConfig = new ProducerConfig();

                using var producer = _adHocKafkaProducerFactory.CreateProducer(producerConfig);

                var message = new Message<string, GenerateReportValue>
                {
                    Key = reportScheduleSummary.FacilityId,
                    Headers = new Headers(),
                    Value = new GenerateReportValue()
                    {
                        ReportId = reportScheduleSummary.ReportId,
                        BypassSubmission = request.BypassSubmission ?? false
                    },
                };

                await producer.ProduceAsync(KafkaTopic.GenerateReportRequested.ToString(), message, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception encountered in FacilityController.RegenerateReport");
                return Problem("An internal server error occurred.", statusCode: 500);
            }

            return Ok();
        }
    }
}
