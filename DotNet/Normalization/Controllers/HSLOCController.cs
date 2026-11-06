﻿﻿using LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Auditing;
using LantanaGroup.Link.Shared.Application.Filters;
using LantanaGroup.Link.Shared.Application.Models;
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

        /// <summary>
        /// The request's audit context. Null when the controller is constructed directly, outside a request.
        /// </summary>
        private IAuditContext? Audit => HttpContext?.RequestServices?.GetService<IAuditContext>();

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
        [Auditable(AuditEventType.Update, "HSLOC",
            "HSLOC updated from version {oldVersion} to {newVersion}: {updated} updated, {added} added, {deactivated} deactivated",
            SuppressPropertyChanges = true)]
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
                var changes = await _hslocManager.Update(model.OldVersion, model.NewVersion, csv, cancellationToken);

                if (Audit is { } audit)
                {
                    //the versions were sanitised above, so the note records what was acted on.
                    audit.Set("oldVersion", model.OldVersion);
                    audit.Set("newVersion", model.NewVersion);
                    audit.Set("updated", changes.Updated);
                    audit.Set("added", changes.Added);
                    audit.Set("deactivated", changes.Deactivated);
                }

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
        [Auditable(AuditEventType.Delete, "HSLOC", "All HSLOC codes deleted ({count} codes)")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ValidateAntiForgeryOrBearerToken]
        public async Task<IActionResult> DeleteAll(CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _hslocManager.DeleteAll(cancellationToken);

                if (Audit is { } audit)
                {
                    audit.Set("count", deleted);
                }

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
        [Auditable(AuditEventType.Delete, "HSLOC", "All HSLOC codes for version {version} deleted ({count} codes)")]
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
                var deleted = await _hslocManager.DeleteByVersion(version, cancellationToken);

                if (Audit is { } audit)
                {
                    //the version was sanitised above, so the note records what was acted on.
                    audit.Set("version", version);
                    audit.Set("count", deleted);
                }

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
        [Auditable(AuditEventType.Delete, "HSLOC", "Id {id} (HSLOC code: {HSLOCCode}, Version: {version}) deleted")]
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
                var deleted = await _hslocManager.DeleteById(id, cancellationToken);

                if (Audit is { } audit)
                {
                    if (deleted is null)
                    {
                        //nothing matched, so the note must not name a code and version as deleted.
                        audit.SuppressNote = true;
                    }
                    else
                    {
                        audit.Set("HSLOCCode", deleted.HSLOCCode);
                        audit.Set("version", deleted.Version);
                    }
                }

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