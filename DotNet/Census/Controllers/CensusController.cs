using Hl7.Fhir.Model;
using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Census.Domain.Managers;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LantanaGroup.Link.Census.Controllers;

[Route("api/census/{facilityId}")]
[Authorize(Policy = PolicyNames.IsLinkAdmin)]
[ApiController]
public class CensusController : Controller
{
    private readonly ILogger<CensusController> _logger;
    private readonly IPatientCensusHistoryManager _patientCensusHistoryManager;
    private readonly ICensusPatientListManager _patientListManager;
    public CensusController(ILogger<CensusController> logger, IPatientCensusHistoryManager patientCensusHistoryManager, ICensusPatientListManager patientListManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _patientCensusHistoryManager = patientCensusHistoryManager ?? throw new ArgumentNullException(nameof(patientCensusHistoryManager));
        _patientListManager = patientListManager ?? throw new ArgumentNullException(nameof(patientListManager));
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
            var history = await _patientCensusHistoryManager.GetPatientCensusHistoryByFacilityId(facilityId);

            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception encountered in CensusController.GetCensusHistory");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
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
            var patients = (await _patientListManager.GetPatientList(facilityId, startDate, endDate)).ToList();

            if(!patients.Any())
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
            _logger.LogError(ex, "Exception encountered in CensusController.GetAdmittedPatients");
            return Problem(
                detail: "An error occurred while retrieving admitted patients.",
                statusCode: StatusCodes.Status500InternalServerError);
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
            if (string.IsNullOrEmpty(facilityId))
            {
                return BadRequest("FacilityId parameter is null or empty.");
            }

            var patients = await _patientListManager.GetPatientListForFacility(facilityId, activeOnly: true);

            return Ok(patients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception encountered in CensusController.GetCurrentCensus");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
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
            var patients = await _patientListManager.GetPatientListForFacility(facilityId, activeOnly: false);

            return Ok(patients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception encountered in CensusController.GetAllPatientsForFacility");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
