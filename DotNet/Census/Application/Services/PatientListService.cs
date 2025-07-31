using LantanaGroup.Link.Census.Application.Factories;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Application.Models.Payloads.Fhir.List;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Census.Domain.Queries;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.DataAcq;

namespace LantanaGroup.Link.Census.Application.Services;

public interface IPatientListService
{
    //Task<IEnumerable<BaseResponse>> ProcessLists(string facilityId, List<PatientListItem> lists, CancellationToken cancellationToken);
    //Task<IEnumerable<BaseResponse>> ProcessList(string facilityId, PatientListItem list, CancellationToken cancellationToken);
    Task ProcessLists(string facilityId, List<PatientListItem> lists, CancellationToken cancellationToken);
    Task ProcessList(string facilityId, PatientListItem list, CancellationToken cancellationToken);
}

public class PatientListService : IPatientListService
{
    private readonly ILogger<PatientListService> _logger;
    private readonly ICensusServiceMetrics _metrics;
    private readonly IPatientEventManager _patientEventManager;
    private readonly IPatientEventQueries _patientEventQueries;

    public PatientListService(
        ILogger<PatientListService> logger,
        ICensusServiceMetrics metrics,
        IPatientEventQueries patientEventQueries,
        IPatientEventManager patientEventManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _patientEventQueries = patientEventQueries ?? throw new ArgumentNullException(nameof(patientEventQueries));
        _patientEventManager = patientEventManager ?? throw new ArgumentNullException(nameof(patientEventManager));
    }

    public async Task ProcessList(string facilityId, PatientListItem list, CancellationToken cancellationToken)
    {
        foreach(var patientId in list.PatientIds)
        {
            var existingEvent = await _patientEventQueries.GetLatestEventByFacilityAndPatientId(facilityId, patientId, cancellationToken);

            if(existingEvent != null && existingEvent.EventType == EventType.FHIRListAdmit && list.ListType == ListType.Admit)
            {
                // If the event already exists, we can skip processing
                _logger.LogInformation("Patient event for {patientId} for FhirListAdmit already exists in facility {facilityId}. Skipping.", patientId, facilityId);
                continue;
            }

            if(existingEvent != null && existingEvent.EventType == EventType.FHIRListDischarge && list.ListType == ListType.Discharge)
            {
                // If the event already exists, we can skip processing
                _logger.LogInformation("Patient event for {patientId} for FhirListDischarge already exists in facility {facilityId}. Skipping.", patientId, facilityId);
                continue;
            }

            if(existingEvent == null && list.ListType == ListType.Discharge)
            {
                //create and add an admit event
                var admitEvent = new FHIRListAdmitPayload(patientId, DateTime.UtcNow).CreatePatientEvent(facilityId, Guid.NewGuid().ToString());
                try
                {
                    var addedAdmitEvent = await _patientEventManager.AddPatientEvent(admitEvent, cancellationToken);
                    _logger.LogInformation("Added admit event for patient {patientId} in facility {facilityId}", patientId, facilityId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error adding admit event for patient {patientId} in facility {facilityId}", patientId, facilityId);
                    throw;
                }
            }

            var patientEvent = list.ListType == ListType.Admit
                ? new FHIRListAdmitPayload(patientId, DateTime.UtcNow).CreatePatientEvent(facilityId, Guid.NewGuid().ToString())
                : new FHIRListDischargePayload(patientId, DateTime.UtcNow).CreatePatientEvent(facilityId, Guid.NewGuid().ToString());

            try
            {
                var addedEvent = await _patientEventManager.AddPatientEvent(patientEvent, cancellationToken);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing patient list for facility {facilityId} and patient {patientId}", facilityId, patientId);
                throw;
            }
        }
    }

    public async Task ProcessLists(string facilityId, List<PatientListItem> lists, CancellationToken cancellationToken)
    {
        foreach(var list in lists)
        {
            try
            {
                await ProcessList(facilityId, list, cancellationToken);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error processing patient list for facility {facilityId}", facilityId);
                // Optionally, you can handle specific exceptions or rethrow them
                throw;
            }
        }
        //var tasks = lists.Select(list => ProcessList(facilityId, list, cancellationToken));
        //await Task.WhenAll(tasks);
        //lists.ForEach(async list => await ProcessList(facilityId, list, cancellationToken));
        //var results = await Task.WhenAll(lists.Select(list => ProcessList(facilityId, list, cancellationToken)));
        //return results.SelectMany(r => r);
    }
}
