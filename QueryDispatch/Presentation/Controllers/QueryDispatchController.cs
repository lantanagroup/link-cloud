using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Application.Models;
using LantanaGroup.Link.QueryDispatch.Application.Queries;
using LantanaGroup.Link.QueryDispatch.Application.QueryDispatchConfiguration.Commands;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.QueryDispatch.Presentation.Controllers
{
    [Route("api/querydispatch")]
    [ApiController]
    public class QueryDispatchController : ControllerBase
    {
        private readonly ILogger<QueryDispatchController> _logger;
        private readonly IQueryDispatchConfigurationFactory _configurationFactory;
        private readonly ICreateQueryDispatchConfigurationCommand _createQueryDispatchConfigurationCommand;
        private readonly IGetQueryDispatchConfigurationQuery _getQueryDispatchConfigurationQuery;
        private readonly IDeleteQueryDispatchConfigurationCommand _deleteQueryDispatchConfigurationCommand;
        private readonly IUpdateQueryDispatchConfigurationCommand _updateQueryDispatchConfigurationCommand;
        private readonly ITenantApiService _tenantApiService;

        public QueryDispatchController(ILogger<QueryDispatchController> logger, IQueryDispatchConfigurationFactory configurationFactory, ICreateQueryDispatchConfigurationCommand createQueryDispatchConfigurationCommand, IGetQueryDispatchConfigurationQuery getQueryDispatchConfigurationQuery, IDeleteQueryDispatchConfigurationCommand deleteQueryDispatchConfigurationCommand, IUpdateQueryDispatchConfigurationCommand updateQueryDispatchConfigurationCommand, ITenantApiService tenantApiService)
        {
            _logger = logger;
            _configurationFactory = configurationFactory;
            _createQueryDispatchConfigurationCommand = createQueryDispatchConfigurationCommand;
            _getQueryDispatchConfigurationQuery = getQueryDispatchConfigurationQuery;
            _deleteQueryDispatchConfigurationCommand = deleteQueryDispatchConfigurationCommand;
            _updateQueryDispatchConfigurationCommand = updateQueryDispatchConfigurationCommand;
            _tenantApiService = tenantApiService;
        }

        //TODO: Daniel - Add authorization policies
        /// <summary>
        /// Gets a facility configuration by facilityId
        /// </summary>
        /// <param name="facilityId"></param>
        /// <returns></returns>
        [HttpGet("configuration/facility/{facilityid}")]
        public async Task<ActionResult<string>> GetFacilityConfiguration(string facilityId) 
        {
            if (string.IsNullOrEmpty(facilityId)) 
            { 
                return BadRequest("No facility id provided."); 
            }

            try
            {
                var config = await _getQueryDispatchConfigurationQuery.Execute(facilityId);

                if (config == null) 
                {
                    return NotFound();
                }

                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError($"An exception occurred while attempting to retrieve the query dispatch configuration of a facility with an id of {facilityId}", ex);
                return StatusCode(500, "An error occurred while attempting to retrieve the facility query dispatch configuration.");
            }
        }

        //TODO: Daniel - Add authorization policies
        /// <summary>
        /// Creates a QueryDispatch configuration record.
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost("configuration")]
        public async Task<ActionResult<RequestResponse>> CreateQueryDispatchConfigurationAsync(QueryDispatchConfiguration model)
        {
            //validate config values
            if (model == null) 
            { 
                return BadRequest("No query dispatch configuration provided."); 
            }

            if (string.IsNullOrWhiteSpace(model.FacilityId))
            {
                _logger.LogError($"Facility Id was not provided in the new query dispatch configuration: {model}.");
                return BadRequest("Facility Id is required in order to create a query dispatch configuration.");
            }

            var existingConfig = await _getQueryDispatchConfigurationQuery.Execute(model.FacilityId);
            if (existingConfig != null)
            {
                _logger.LogError($"Query dispatch configuration for Facility Id {model.FacilityId} was already created: {model}.");
                return BadRequest($"FacilityID {model.FacilityId} configuration was already created.");
            }

            try
            {
                var facilityCheckResult = await _tenantApiService.CheckFacilityExists(model.FacilityId);
                    if (!facilityCheckResult)
                        return BadRequest($"Facility {model.FacilityId} does not exist.");

                var config =
                    _configurationFactory.CreateQueryDispatchConfiguration(model.FacilityId,
                        model.DispatchSchedules);

                await _createQueryDispatchConfigurationCommand.Execute(config);

                var response = new RequestResponse
                {
                    Id = config.Id,
                    Message = "The query dispatch configuration was created successfully."
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"An exception occurred while attempting to create a new query dispatch configuration - {model}", ex);
                return StatusCode(500, "An error occurred while attempting to create a new query dispatch configuration.");
            }
        }

        //TODO: Daniel - Add authorization policies
        /// <summary>
        /// Deletes a QueryDispatch configuration record.
        /// </summary>
        /// <param name="facilityId"></param>
        /// <returns></returns>
        [HttpDelete("configuration/facility/{facilityid}")]
        public async Task<ActionResult<RequestResponse>> DeleteQueryDispatchConfiguration(string facilityId)
        {
            if (string.IsNullOrEmpty(facilityId)) 
            { 
                return BadRequest("No facility id provided."); 
            }

            try
            {
                bool result = await _deleteQueryDispatchConfigurationCommand.Execute(facilityId);

                RequestResponse response = new RequestResponse()
                {
                    Id = facilityId,
                    Message = result ? $"Query Dispatch configuration {facilityId} was deleted succesfully." : $"Failed to delete Query Dispatch configuration {facilityId}, check log for details."
                };
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"An exception occurred while attempting to delete the query dispatch configuration for facility id {facilityId}", ex);
                return StatusCode(500, "An error occurred while attempting to delete the facility query dispatch configuration.");
            }
        }

        //TODO: Daniel - Add authorization policies
        /// <summary>
        /// Updates a QueryDispatch configuration record.
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPut("configuration")]
        public async Task<ActionResult<RequestResponse>> UpdateQueryDispatchConfiguration(QueryDispatchConfiguration model)
        {
            if (model == null)
            {
                return BadRequest("No query dispatch configuration provided.");
            }

            if (string.IsNullOrWhiteSpace(model.FacilityId))
            {
                _logger.LogError($"Facility Id was not provided in the update query dispatch configuration: {model}.");
                return BadRequest("Facility Id is required in order to update a query dispatch configuration.");
            }

            var existingConfig = await _getQueryDispatchConfigurationQuery.Execute(model.FacilityId);
            if (existingConfig == null)
            {
                _logger.LogError($"Query dispatch configuration for Facility Id {model.FacilityId} does not exist.");
                return BadRequest($"FacilityID {model.FacilityId} configuration does not exist.");
            }

            try
            {
                var facilityCheckResult = await _tenantApiService.CheckFacilityExists(model.FacilityId);
                if (!facilityCheckResult)
                    return BadRequest($"Facility {model.FacilityId} does not exist.");

                await _updateQueryDispatchConfigurationCommand.Execute(existingConfig, model.DispatchSchedules);

                var response = new RequestResponse
                {
                    Id = existingConfig.Id,
                    Message = "The query dispatch configuration was updated successfully."
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"An exception occurred while attempting to update the query dispatch configuration - {model}", ex);
                return StatusCode(500, "An error occurred while attempting to update the query dispatch configuration.");
            }
        }
    }
}
