using Hl7.Fhir.Model;
using LantanaGroup.Link.Census.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Services.Security;
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
    private readonly IPatientEncounterQueries _patientEncounterQueries;
    private readonly IPatientEventQueries _patientEventQueries;

    public CensusController(ILogger<CensusController> logger, IPatientEncounterQueries patientEncounterQueries, IPatientEventQueries patientEventQueries)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _patientEncounterQueries = patientEncounterQueries ?? throw new ArgumentNullException(nameof(_patientEncounterQueries));
        _patientEventQueries = patientEventQueries ?? throw new ArgumentNullException(nameof(_patientEventQueries));
    }

    /// <summary>
    /// Gets the admitted patients for a facility within a date range.
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
    public async Task<ActionResult<Hl7.Fhir.Model.List>> GetAdmittedPatients(string facilityId, DateTime startDate, DateTime endDate)
    {
        facilityId = HtmlInputSanitizer.Sanitize(facilityId);
        if (string.IsNullOrWhiteSpace(facilityId))
            return BadRequest("facilityId is required.");

        if (startDate > endDate)
            return BadRequest("startDate must be less than or equal to endDate.");

        try
        {
            var patients = (await _patientEncounterQueries.GetAdmittedPatientEncounterModelsByDateRange(facilityId, startDate, endDate))?.ToList();

            if (patients == null || !patients.Any())
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

                var identifier = patient.PatientIdentifiers.FirstOrDefault();

                if (identifier != null)
                {
                    fhirList.Entry.Add(new List.EntryComponent()
                    {
                        Item = new ResourceReference(identifier.Identifier.StartsWith("Patient/") ? identifier.Identifier : $"Patient/" + identifier.Identifier)
                    });
                }
            }

            return Ok(fhirList);
        }
        catch (ArgumentException argEx)
        {
            _logger.LogError(argEx, "Invalid argument in CensusController.GetAdmittedPatients");
            return BadRequest(argEx.Message);
        }
        catch (InvalidOperationException invOpEx)
        {
            _logger.LogError(invOpEx, "Invalid operation in CensusController.GetAdmittedPatients");
            return BadRequest(invOpEx.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception encountered in CensusController.GetAdmittedPatients");
            return Problem(detail: "An error occurred while retrieving facility admitted patients.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}