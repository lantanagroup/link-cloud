using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using FluentValidation;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.DataAcquisition.Controllers;

[Route("api/data")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[ApiController]
[Tags("SftpConfiguration")]
public class SftpConnectionTestController(
    ISftpConnectionTestService connectionTestService,
    IValidator<SftpTestConnectionRequestModel> validator,
    ILogger<SftpConnectionTestController> logger) : Controller
{
    private readonly ISftpConnectionTestService _connectionTestService = connectionTestService;
    private readonly IValidator<SftpTestConnectionRequestModel> _validator = validator;
    private readonly ILogger<SftpConnectionTestController> _logger = logger;

    /// <summary>
    /// Tests an ad-hoc SFTP connection using connection details supplied directly in the request, without requiring a saved configuration.
    /// Optionally lists files found in the report directory and, for each file, a preview of the patients it contains.
    /// </summary>
    /// <param name="model">The SFTP connection details to test.</param>
    /// <param name="includeFileContent">When true, includes a files element listing the files found in reportDirectory, along with a preview of the patients found in each file. Defaults to false.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("sftp-configurations/test-connection")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SftpTestConnectionResult))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [Description("Tests an ad-hoc SFTP connection using connection details supplied directly in the request, without requiring a saved configuration.\nOptionally lists files found in the report directory and, for each file, a preview of the patients it contains.")]
    public async Task<ActionResult<SftpTestConnectionResult>> TestConnection(
        [FromBody][Required] SftpTestConnectionRequestModel model,
        [FromQuery] bool includeFileContent = false,
        CancellationToken cancellationToken = default)
    {
        // FluentValidation isn't wired into MVC model validation, so the validator has to be run explicitly
        var validationResult = await _validator.ValidateAsync(model, cancellationToken);
        if (!validationResult.IsValid)
        {
            foreach (var error in validationResult.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }

            return ValidationProblem(ModelState);
        }

        try
        {
            var result = await _connectionTestService.TestSftpConnectionAsync(
                model.HostName,
                model.HostUrlPort,
                model.Username,
                model.Password,
                model.ReportDirectory,
                includeFileContent,
                cancellationToken);

            return Ok(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error testing SFTP connection for host {HostName}:{Port}", model.HostName.SanitizeForLog(), model.HostUrlPort);
            return Problem(
                title: "Internal Server Error",
                detail: $"An unexpected error occurred while testing the SFTP connection. Trace Id: {HttpContext.TraceIdentifier}",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}