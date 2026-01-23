using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Interfaces.Models;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.DataAcquisition.Controllers;


[Route("api/data/sftp-logs")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[ApiController]
public class SftpLogController : ControllerBase
{
    private readonly ILogger<SftpLogController> _logger;
    private readonly ISftpAcquisitionLogManager _manager;
    private readonly ISftpAcquisitionLogQueries _queries;
    private readonly ITenantApiService _tenantApiService;

    private const int DefaultLogPageSize = 20;
    private const string DefaultSortBy = "ProcessDate";
    
    public SftpLogController(ILogger<SftpLogController> logger, ISftpAcquisitionLogManager manager, ISftpAcquisitionLogQueries queries, ITenantApiService tenantApiService)
    {
        _logger = logger;
        _manager = manager;
        _queries = queries;
        _tenantApiService = tenantApiService;
    }
    
    /// <summary>
    /// Search SFTP logs
    /// </summary>
    /// <param name="queryParameters"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IPagedModel<SftpAcquisitionLogModel>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IPagedModel<SftpAcquisitionLogModel>>> Search(
        [FromQuery] SftpLogSearchParameters? queryParameters, CancellationToken cancellationToken = default)
    {
        // If no query parameters are specified, use default values
        queryParameters ??= new SftpLogSearchParameters
        {
            SortBy = DefaultSortBy,
            PageSize = DefaultLogPageSize
        };
        
        // Store httpContext so that it is not lost during processing
        var httpContext = HttpContext;

        try
        {
            var searchResults = await _queries.SearchAsync(queryParameters, cancellationToken);
            
            return Ok(searchResults);
        }
        catch (Exception)
        {
            return Problem(
                title: "An error occurred while processing your request.",
                detail: $"An unexpected error occurred while processing your request, please see the logs for more details. TraceId: {httpContext.TraceIdentifier}",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
    
    /// <summary>
    /// Get SFTP log by external ID
    /// </summary>
    /// <param name="logId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("{logId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SftpAcquisitionLogModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SftpAcquisitionLogModel>> GetSftpLogByExternalId(string logId, CancellationToken cancellationToken = default)
    {
        // Validate log ID
        if (string.IsNullOrWhiteSpace(logId) || !Guid.TryParse(logId, out var id))
        {
            return BadRequest("Invalid SFTP Acquisition log ID.");
        }
        
        // Store httpContext so that it is not lost during processing
        var httpContext = HttpContext;

        try
        {
            var log = await _queries.GetByExternalIdAsync(id, cancellationToken);
        
            if (log is null)
                return NotFound();
        
            return Ok(log.ToModel());
        }
        catch (Exception)
        {
            return Problem(
                title: "An error occurred while processing your request.",
                detail: $"An unexpected error occurred while processing your request, please see the logs for more details. TraceId: {httpContext.TraceIdentifier}",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
    
    /// <summary>
    /// Create SFTP log
    /// </summary>
    /// <param name="req"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(SftpAcquisitionLogModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> CreateSftpLog(CreateSftpLogRequest req, CancellationToken cancellationToken = default)
    {
        // Store httpContext so that it is not lost during processing
        var httpContext = HttpContext;
        
        // validate user access to organization before proceeding - future enhancement

        try
        {
            // Sanitize organizationId
            var organizationId = req.FacilityId.SanitizeAndRemove();

            // Verify that the facility/organization exists
            var facilityExists = await _tenantApiService.CheckFacilityExists(organizationId, cancellationToken);

            if (!facilityExists)
            {
                return BadRequest($"No facility found for organizationId: {organizationId}");
            }

            var createdLog = await _manager.CreateAsync(req.ToModel(), cancellationToken);

            return Created($"/api/data/sftp-logs/{createdLog.ExternalId}", createdLog);

        }
        catch (MissingFacilityIdException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception)
        {
            return Problem(
                title: "An error occurred while processing your request.",
                detail:
                $"An unexpected error occurred while processing your request, please see the logs for more details. TraceId: {httpContext.TraceIdentifier}",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    /// <summary>
    /// Update Process Date of SFTP log
    /// </summary>
    /// <param name="logId"></param>
    /// <param name="req"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPut("{logId}")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(SftpAcquisitionLogModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> UpdateSftpLogProcessDate(string logId, UpdateSftpLogRequest req, CancellationToken cancellationToken = default)
    {
        // Validate log ID
        if (string.IsNullOrWhiteSpace(logId) || !Guid.TryParse(logId, out var id))
        {
            return BadRequest("Invalid log ID.");
        }
        
        if (id == Guid.Empty)
        {
            return BadRequest("Invalid SFTP Acquisition Log Id.");
        }
        
        // Validate the request model id matches the id in the url
        if (req.ExternalId != id)
        {
            _logger.LogWarning("Sftp Log id in the request body ({RequestExternalId}) does not match the id in the url ({ExternalId}).", req.ExternalId, logId.Sanitize());
            return BadRequest("The ids in the request body and url do not match.");
        }
        
        // Store httpContext so that it is not lost during processing
        var httpContext = HttpContext;
        
        // validate user access to organization before proceeding - future enhancement

        try
        {
            var updatedLog = await _manager.UserUpdateAsync(req.ToModel(), cancellationToken);

            return Accepted(updatedLog);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (MissingFacilityIdException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (DomainEntityNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception)
        {
            return Problem(
                title: "An error occurred while processing your request.",
                detail:
                $"An unexpected error occurred while processing your request, please see the logs for more details. TraceId: {httpContext.TraceIdentifier}",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }
}