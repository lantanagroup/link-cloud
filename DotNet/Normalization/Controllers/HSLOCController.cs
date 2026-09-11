﻿﻿using LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Filters;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.Normalization.Controllers
{
    [Route("api/normalization/[controller]")]
    [ApiController]
    [Authorize(Policy = PolicyNames.IsLinkAdmin)]
    public class HSLOCController : ControllerBase
    {
        private readonly IHSLOCManager _hslocManager;
        private readonly IHSLOCQueries _hslocQueries;

        public HSLOCController(IHSLOCManager hslocManager, IHSLOCQueries hslocQueries)
        {
            _hslocManager = hslocManager;
            _hslocQueries = hslocQueries;
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<HSLOC>))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<HSLOC>>> GetAll(
            bool includeInactive = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return Ok(await _hslocQueries.GetAll(includeInactive, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                return Problem(detail: exception.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPut]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ValidateAntiForgeryOrBearerToken]
        public async Task<IActionResult> Update([FromForm] PutHSLOCModel model, CancellationToken cancellationToken)
        {
            model.OldVersion = model.OldVersion.Sanitize();
            model.NewVersion = model.NewVersion.Sanitize();

            if (model.CsvFile is null || model.CsvFile.Length == 0)
            {
                return Problem(detail: "A non-empty HSLOC CSV file must be provided.", statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                await using var csv = model.CsvFile.OpenReadStream();
                await _hslocManager.Update(model.OldVersion, model.NewVersion, csv, cancellationToken);
                return NoContent();
            }
            catch (ArgumentException exception)
            {
                return Problem(detail: exception.Message, statusCode: StatusCodes.Status400BadRequest);
            }
            catch (InvalidOperationException exception)
            {
                return Problem(detail: exception.Message, statusCode: StatusCodes.Status409Conflict);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                return Problem(detail: exception.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ValidateAntiForgeryOrBearerToken]
        public async Task<IActionResult> DeleteAll(CancellationToken cancellationToken)
        {
            try
            {
                await _hslocManager.DeleteAll(cancellationToken);
                return NoContent();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                return Problem(detail: exception.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpDelete("versions/{version}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ValidateAntiForgeryOrBearerToken]
        public async Task<IActionResult> DeleteByVersion(string version, CancellationToken cancellationToken)
        {
            version = version.SanitizeAndRemove();

            if (string.IsNullOrWhiteSpace(version))
            {
                return Problem(detail: "An HSLOC version must be provided.", statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                await _hslocManager.DeleteByVersion(version, cancellationToken);
                return NoContent();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                return Problem(detail: exception.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ValidateAntiForgeryOrBearerToken]
        public async Task<IActionResult> DeleteById(Guid id, CancellationToken cancellationToken)
        {
            if (id == Guid.Empty)
            {
                return Problem(detail: "A valid HSLOC identifier must be provided.", statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                await _hslocManager.DeleteById(id, cancellationToken);
                return NoContent();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                return Problem(detail: exception.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}