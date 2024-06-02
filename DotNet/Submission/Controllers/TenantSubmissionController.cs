using LantanaGroup.Link.Submission.Application.Interfaces;
using LantanaGroup.Link.Submission.Application.Models.ApiModels;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.Submission.Controllers
{
    [Route("api/[controller]")]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    public class TenantSubmissionController : Controller
    {
        private ITenantSubmissionManager _tenantSubmissionManager { get; set; }

        public TenantSubmissionController(ITenantSubmissionManager tenantSubmissionManager)
        {
            _tenantSubmissionManager = tenantSubmissionManager;
        }

        /// <summary>
        /// Get the TenantSubmissionConfig for the provided Facility Id
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="cancellationToken"></param>
        [HttpGet]
        [Route("Find")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TenantSubmissionConfig))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<TenantSubmissionConfig>> FindTenantSubmissionConfig(string facilityId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                return BadRequest();
            }

            var retVal = await _tenantSubmissionManager.FindTenantSubmissionConfig(facilityId, cancellationToken);

            if (retVal != null)
            {
                return Ok(retVal);
            }
            else
            {
                return Problem($"TenantSubmissionConfig for FacilityId {facilityId} not found.", statusCode: 304);
            }
        }

        /// <summary>
        /// Get the TenantSubmissionConfig for the provided TenantSubmissionConfig Id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="cancellationToken"></param>
        [HttpGet]
        [Route("Get")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TenantSubmissionConfig))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<TenantSubmissionConfig>> GetTenantSubmissionConfig(string configId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(configId))
            {
                return BadRequest();
            }

            var retVal = await _tenantSubmissionManager.GetTenantSubmissionConfig(configId, cancellationToken);

            if (retVal != null)
            {
                return Ok(retVal);
            }
            else
            {
                return Problem($"TenantSubmissionConfig {configId} not found.", statusCode: 304);
            }
        }

        /// <summary>
        /// Delete the TenantSubmissionConfig for the provided TenantSubmissionConfig Id
        /// </summary>
        /// <param name="configId"></param>
        /// <param name="cancellationToken"></param>
        [HttpDelete]
        [Route("Delete")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status304NotModified)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<bool>> DeleteTenantSubmissionConfig(string configId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(configId))
            {
                return BadRequest("configId is null or white space.");
            }

            try
            {
                var retVal = await _tenantSubmissionManager.DeleteTenantSubmissionConfig(configId, cancellationToken);
                return Ok(retVal);
            }
            catch {}

            return Problem($"TenantSubmissionConfig {configId} not found.", statusCode: 304);
        }

        /// <summary>
        /// Create a TenantSubmissionConfig
        /// </summary>
        /// <param name="tenantSubmissionConfig"></param>
        [HttpPost]
        [Route("Create")]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(TenantSubmissionConfig))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status304NotModified)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<TenantSubmissionConfig>> CreateTenantSubmissionConfig([FromBody] TenantSubmissionConfig tenantSubmissionConfig)
        {
            var createdConfig = await _tenantSubmissionManager.CreateTenantSubmissionConfig(tenantSubmissionConfig);
            if (createdConfig != null)
            {
                return Created(createdConfig.Id, createdConfig);
            }
            else
            {
                return Problem("Unable to create the TenantSubmissionConfig", statusCode: 304);
            }
        }

        /// <summary>
        /// Update an existing TenantSubmissionConfig
        /// </summary>
        /// <param name="tenantSubmissionConfig"></param>
        /// <param name="cancellationToken"></param>
        [HttpPut]
        [Route("Update")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TenantSubmissionConfig))]
        [ProducesResponseType(StatusCodes.Status304NotModified, Type = typeof(TenantSubmissionConfig))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<TenantSubmissionConfig>> UpdateTenantSubmissionConfig([FromBody] TenantSubmissionConfig tenantSubmissionConfig, CancellationToken cancellationToken = default)
        {
            var updatedConfig = await _tenantSubmissionManager.UpdateTenantSubmissionConfig(tenantSubmissionConfig, cancellationToken);
            if (updatedConfig != null)
            {
                return Ok(updatedConfig);
            }
            else
            {
                return Problem($"TenantSubmissionConfig {tenantSubmissionConfig.Id} not found.", statusCode:304);
            }
        }
    }
}
