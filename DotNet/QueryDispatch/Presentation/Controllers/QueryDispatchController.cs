﻿
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Application.Models;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Services;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QueryDispatch.Application.Settings;
using QueryDispatch.Domain.Managers;
using System.Threading;

namespace LantanaGroup.Link.QueryDispatch.Presentation.Controllers
{
    [Route("api/querydispatch")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    [ApiController]
    public class QueryDispatchController : ControllerBase
    {
        private readonly ILogger<QueryDispatchController> _logger;
        private readonly IQueryDispatchConfigurationFactory _configurationFactory;

        private readonly ITenantApiService _tenantApiService;
        private readonly IQueryDispatchConfigurationRepository _queryDispatchConfigRepo;
        private readonly QueryDispatchConfigurationManager _queryDispatchConfigurationManager;

        public QueryDispatchController(ILogger<QueryDispatchController> logger, IQueryDispatchConfigurationFactory configurationFactory, ITenantApiService tenantApiService, IQueryDispatchConfigurationRepository queryDispatchConfigRepo, QueryDispatchConfigurationManager queryDispatchConfigurationManager)
        {
            _logger = logger;
            _configurationFactory = configurationFactory;
            _tenantApiService = tenantApiService;
            _queryDispatchConfigRepo = queryDispatchConfigRepo;
            _queryDispatchConfigurationManager = queryDispatchConfigurationManager;
        }

        /// <summary>
        /// Gets a Query Dispatch facility configuration by facilityId
        /// </summary>
        /// <param name="facilityId"></param>
        /// <returns>
        /// Success: 200 
        /// Bad Request: 400
        /// Not Found: 404
        /// Server Error: 500
        /// </returns>
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(QueryDispatchConfigurationEntity))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet("configuration/facility/{facilityid}")]
        public async Task<ActionResult<string>> GetFacilityConfiguration(string facilityId) 
        {
            if (string.IsNullOrEmpty(facilityId)) 
            { 
                return BadRequest("No facility id provided."); 
            }

            try
            {
                //var config = await _getQueryDispatchConfigurationQuery.Execute(facilityId);
                var config = await _queryDispatchConfigRepo.FirstOrDefaultAsync(x => x.FacilityId == facilityId);              

                if (config == null) 
                {
                    return NotFound();
                }

                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(QueryDispatchConstants.LoggingIds.GetItem, "Get QueryDispatch configuration"), ex, "An exception occurred while attempting to retrieve a QueryDispatch configuration for facility {facilityId}", facilityId);

                throw;
            }
        }

        /// <summary>
        /// Creates a QueryDispatch configuration record.
        /// </summary>
        /// <param name="model"></param>
        /// <returns>
        /// Created: 201
        /// Bad Request: 400
        /// Server Error: 500
        /// </returns>
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(QueryDispatchConfigurationEntity))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPost("configuration")]
        public async Task<ActionResult<RequestResponse>> CreateQueryDispatchConfigurationAsync(QueryDispatchConfiguration model, CancellationToken cancellationToken)
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

            foreach (var schedule in model.DispatchSchedules)
            {
                if (!IsDurationFormatValid(schedule.Duration))
                {
                    _logger.LogError($"Duration format is invalid: {schedule.Duration}.");
                    return BadRequest("Duration format is invalid: " + schedule.Duration + ". Please provide a valid duration format.");
                }
            }

            var existingConfig = await _queryDispatchConfigRepo.FirstOrDefaultAsync(x => x.FacilityId == model.FacilityId);

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

                var config = _configurationFactory.CreateQueryDispatchConfiguration(model.FacilityId, model.DispatchSchedules);


                await _queryDispatchConfigurationManager.AddConfigEntity(config, cancellationToken);


                return Created(config.Id, config);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(QueryDispatchConstants.LoggingIds.UpdateItem, "Post QueryDispatch configuration"), ex, "An exception occurred while attempting to save a QueryDispatch configuration for facility " + model.FacilityId);

                throw;
            }
        }

        /// <summary>
        /// Deletes a QueryDispatch configuration record.
        /// </summary>
        /// <param name="facilityId"></param>
        /// <returns>
        /// No Content: 204
        /// Server Error: 500
        /// </returns>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpDelete("configuration/facility/{facilityId}")]
        public async Task<ActionResult<RequestResponse>> DeleteQueryDispatchConfiguration(string facilityId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(facilityId)) 
            { 
                return BadRequest("No facility id provided."); 
            }

            try
            {
                await _queryDispatchConfigurationManager.DeleteConfigEntity(facilityId, cancellationToken);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(QueryDispatchConstants.LoggingIds.DeleteItem, "Delete QueryDispatch configuration"), ex, "An exception occurred while attempting to delete a QueryDispatch configuration for facility " + facilityId);

                throw;
            }
        }

        /// <summary>
        /// Updates a QueryDispatch configuration record.
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="model"></param>
        /// <returns>
        /// Created: 201
        /// No Content: 204
        /// Bad Request: 400
        /// Server Error: 500
        /// </returns>
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(QueryDispatchConfigurationEntity))]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPut("configuration/facility/{facilityId}")]
        public async Task<ActionResult<RequestResponse>> UpdateQueryDispatchConfiguration(string facilityId, QueryDispatchConfiguration model, CancellationToken cancellationToken)
        {
            if (model == null)
            {
                return BadRequest("No query dispatch configuration provided.");
            }

            if (string.IsNullOrWhiteSpace(facilityId))
            {
                _logger.LogError($"Facility Id was not provided in the update query dispatch configuration: {model}.");
                return BadRequest("Facility Id is required in order to update a query dispatch configuration.");
            }

            foreach (var schedule in model.DispatchSchedules)
            {
                if (!IsDurationFormatValid(schedule.Duration))
                {
                    _logger.LogError($"Duration format is invalid: {schedule.Duration}.");
                    return BadRequest("Duration format is invalid: " + schedule.Duration + ". Please provide a valid duration format.");
                }
            }

            try
            {
                var facilityCheckResult = await _tenantApiService.CheckFacilityExists(facilityId);

                if (!facilityCheckResult)
                {
                    return BadRequest($"Facility {facilityId} does not exist.");
                }

                var existingConfig = await _queryDispatchConfigRepo.FirstOrDefaultAsync(x => x.FacilityId == facilityId);

                if (existingConfig == null)
                {
                    var config = _configurationFactory.CreateQueryDispatchConfiguration(facilityId, model.DispatchSchedules);
                    await _queryDispatchConfigRepo.AddAsync(config);

                    return Created(config.Id, config);
                }
                else
                {
                    await _queryDispatchConfigurationManager.SaveConfigEntity(existingConfig, model.DispatchSchedules, cancellationToken);
                    return NoContent();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(QueryDispatchConstants.LoggingIds.UpdateItem, "Put QueryDispatch configuration"), ex, "An exception occurred while attempting to update a QueryDispatch configuration for facility " + facilityId);

                throw;
            }
        }

        private bool IsDurationFormatValid(string duration)
        {
            try
            {
                System.Xml.XmlConvert.ToTimeSpan(duration);
            }
            catch             {
                return false;
            }

            return true;
        }
    }
}
