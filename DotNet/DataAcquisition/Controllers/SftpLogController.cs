using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.Shared.Application.Interfaces.Models;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.DataAcquisition.Controllers;


[Microsoft.AspNetCore.Components.Route("api/data/sftp-logs")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[ApiController]
public class SftpLogController : ControllerBase
{
    private readonly ILogger<SftpLogController> _logger;
    private readonly ISftpAcquisitionLogManager _manager;
    private readonly ISftpAcquisitionLogQueries _queries;

    private const int DefaultLogPageSize = 20;
    private const string DefaultSortBy = "ProcessDate";
    
    public SftpLogController(ILogger<SftpLogController> logger, ISftpAcquisitionLogManager manager, ISftpAcquisitionLogQueries queries)
    {
        _logger = logger;
        _manager = manager;
        _queries = queries;
    }
    
    /// <summary>
    /// Get SFTP log by ID
    /// </summary>
    /// <param name="id"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SftpAcquisitionLogModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SftpAcquisitionLogModel>> GetSftpLogById(long id, CancellationToken cancellationToken = default)
    {
        // Validate log ID
        if (id <= 0)
            return BadRequest("Invalid log ID.");
        
        // Store httpContext so that it is not lost during processing
        var httpContext = HttpContext;

        try
        {
            var log = await _queries.GetByIdAsync(id, cancellationToken);
        
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
    /// Search SFTP logs
    /// </summary>
    /// <param name="queryParameters"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet()]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IPagedModel<SftpAcquisitionLogModel>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IPagedModel<SftpAcquisitionLogModel>>> Search(
        SftpLogSearchParameters? queryParameters, CancellationToken cancellationToken = default)
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


        return Ok();
    }
}