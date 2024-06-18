using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Census.Application.Commands;
using LantanaGroup.Link.Census.Application.Settings;
using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Settings;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Census.Controllers;

[Route("api/census/{facilityId}")]
[ApiController]
public class CensusController : Controller
{
    private readonly ILogger<CensusController> _logger;
    private readonly IMediator _mediator;

    public CensusController(ILogger<CensusController> logger, IMediator mediator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// Gets Patient List history for a facility.
    /// </summary>
    /// <param name="facilityId"></param>
    /// <returns>
    ///     Success: 200
    ///     Server Error: 500
    /// </returns>
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<PatientCensusHistoricEntity>))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<PatientCensusHistoricEntity>>> GetCensusHistory(string facilityId)
    {
        try
        {
            var history = await _mediator.Send(new GetCensusHistoryQuery
            {
                FacilityId = facilityId
            });
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(LoggingIds.GetItem, "Get Census History"), ex, "An exception occurred while attempting to get census history with an id of {id}", facilityId);
            throw;
        }
    }

    /// <summary>
    /// Gets the admitted patients for a facility within a date range. If no dates are provided, it will return all active patients.
    /// </summary>
    /// <param name="facilityId"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns>
    ///     Success: 200
    ///     Server Error: 500
    /// </returns>
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Hl7.Fhir.Model.List))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [HttpGet("history/admitted")]
    public async Task<ActionResult<Hl7.Fhir.Model.List>> GetAdmittedPatients(string facilityId, DateTime startDate = default, DateTime endDate = default)
    {
        try
        {
            var patients = (await _mediator.Send(new GetAdmittedPatientsQuery
            {
                FacilityId = facilityId,
                StartDate = startDate,
                EndDate = endDate
            })).DistinctBy(p => p.PatientId).ToList();

            if(patients.Count == 0)
            {
                return NotFound($"No patients found for facilityId {facilityId}");
            }

            var fhirList = new Hl7.Fhir.Model.List();
            fhirList.Status = List.ListStatus.Current;
            fhirList.Mode = ListMode.Snapshot;
            fhirList.Extension.Add(new Extension()
            {
                Url = "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/link-patient-list-applicable-period-extension",
                Value = new Period()
                {
                    StartElement = new FhirDateTime(new DateTimeOffset(startDate)),
                    EndElement = new FhirDateTime(new DateTimeOffset(endDate))
                }
            });

            foreach (var patient in patients)
            {
                fhirList.Entry.Add(new List.EntryComponent()
                {
                    Item = new ResourceReference(patient.PatientId.StartsWith("Patient/") ? patient.PatientId : "Patient/" + patient.PatientId)
                });
            }

            return Ok(fhirList);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(LoggingIds.GetItem, "Get Admitted Patients"), ex, "An exception occurred while attempting to get admitted patients with an id of {id}", facilityId);
            throw;
        }
    }

    /// <summary>
    /// Gets the current census for a facility.
    /// </summary>
    /// <param name="facilityId"></param>
    /// <returns>
    ///     Success: 200
    ///     Server Error: 500
    /// </returns>
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<CensusPatientListEntity>))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [HttpGet("current")]
    public async Task<ActionResult<List<CensusPatientListEntity>>> GetCurrentCensus(string facilityId)
    {
        try
        {
            var patients = await _mediator.Send(new GetCurrentCensusQuery
            {
                FacilityId = facilityId
            });
            return Ok(patients);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(LoggingIds.GetItem, "Get Current Census"), ex, "An exception occurred while attempting to get current census with an id of {id}", facilityId);
            throw;
        }
    }

    /// <summary>
    /// Gets all patient list records for a facility.
    /// </summary>
    /// <param name="facilityId"></param>
    //// <returns>
    ///     Success: 200
    ///     Server Error: 500
    /// </returns>
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<CensusPatientListEntity>))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [HttpGet("all")]
    public async Task<ActionResult<List<CensusPatientListEntity>>> GetAllPatientsForFacility(string facilityId)
    {
        try
        {
            var patients = await _mediator.Send(new GetAllPatientsForFacilityQuery
            {
                FacilityId = facilityId
            });
            return Ok(patients);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(LoggingIds.GetItem, "Get All Patients For Facility"), ex, "An exception occurred while attempting to get All Patients For Facility with an id of {id}", facilityId);
            throw;
        }
    }
}
