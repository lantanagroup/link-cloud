using Hl7.Fhir.Model;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Messages;
using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Census.Domain.Queries;
using LantanaGroup.Link.Census.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Data.SqlClient;

namespace LantanaGroup.Link.Census.Application.Services;

public interface IPatientIdsAcquiredService
{
    Task<IEnumerable<BaseResponse>> ProcessEvent(ConsumePatientIdsAcquiredEventModel request, CancellationToken cancellationToken);
}

public class PatientIdsAcquiredService : IPatientIdsAcquiredService
{
    private readonly ILogger<PatientIdsAcquiredService> _logger;
    private readonly ICensusPatientListManager _patientListManager;
    private readonly ICensusPatientListQueries _patientListQueries;
    private readonly IPatientCensusHistoryManager _historyManager;
    private readonly ICensusServiceMetrics _metrics;

    public PatientIdsAcquiredService(
        ILogger<PatientIdsAcquiredService> logger,
        ICensusPatientListManager patientListRepository,
        IPatientCensusHistoryManager historyRepository,
        ICensusPatientListQueries patientListQueries,
        ICensusServiceMetrics metrics)
    {
        _logger = logger;
        _patientListManager = patientListRepository;
        _patientListQueries = patientListQueries;
        _historyManager = historyRepository;
        _metrics = metrics;
    }

    public async Task<IEnumerable<BaseResponse>> ProcessEvent(ConsumePatientIdsAcquiredEventModel request, CancellationToken cancellationToken)
    {
        // 1. convert Fhir List to patient entities
        // 2. get existing census from database
        // 3. compare:
        //    - new patients need admitted date updated to DateTime.UtcNow
        //    - patients that are on existing list and not in new list need to have discharged date set to DateTime.UtcNow
        // 4. save updated/new patients   
        var convertedList = ConvertFhirListToEntityList(request.Message.PatientIds, request.FacilityId);

        List<CensusPatientListModel>? activePatients = null;
        try
        {
            activePatients = (await _patientListQueries.SearchAsync(new SearchCensusPatientListModel
            {
                FacilityId = request.FacilityId,
                ActiveOnly = true,
                PageSize = int.MaxValue
            }, cancellationToken)).Records;
        }
        catch(SqlException ex)
        {
            _logger.LogError(ex, "Error getting active patients for facility {FacilityId}", request.FacilityId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active patients for facility {FacilityId}", request.FacilityId);
            throw;
        }

        var patientsModified = new List<CensusPatientListModel>();

        //find new patients
        foreach (var patient in convertedList)
        {
            if (!activePatients.Any(x => x.PatientId == patient.PatientId))
            {
                var existingPatient = (await _patientListQueries.SearchAsync(new SearchCensusPatientListModel
                {
                    FacilityId = request.FacilityId,
                    PatientId = patient.PatientId,
                    PageSize = int.MaxValue
                }, cancellationToken)).Records.SingleOrDefault();

                if (existingPatient != null)
                {
                    await _patientListManager.UpdateAsync(new UpdateCensusPatientListModel
                    {
                        FacilityId = existingPatient.FacilityId,
                        PatientId = existingPatient.PatientId,
                        AdmitDate = DateTime.UtcNow,
                        IsDischarged = false,
                        DischargeDate = null                        
                    }, cancellationToken);
                    patientsModified.Add(existingPatient);
                }
                else
                {
                    patient.AdmitDate = DateTime.UtcNow;

                    var result = await _patientListManager.CreateAsync(new CreateCensusPatientListModel
                    {
                        FacilityId = request.FacilityId,
                        PatientId = patient.PatientId,
                        AdmitDate = patient.AdmitDate,
                        DischargeDate = null,
                        IsDischarged = false,
                        DisplayName = patient.DisplayName
                    }, cancellationToken);

                    patientsModified.Add(result);
                }

                _metrics.IncrementPatientAdmittedCounter([ 
                    new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, request.FacilityId),
                    new KeyValuePair<string, object?>(DiagnosticNames.PatientId, patient.PatientId),
                    new KeyValuePair<string, object?>(DiagnosticNames.PatientEvent, PatientEvents.Admit.ToString())
                ]);
            }
        }

        //find discharged patients
        foreach (var patient in activePatients)
        {
            if (!convertedList.Any(x => x.PatientId == patient.PatientId))
            {
                patient.IsDischarged = true;
                patient.DischargeDate = DateTime.UtcNow;
                patient.ModifyDate = DateTime.UtcNow;

                await _patientListManager.UpdateAsync(new UpdateCensusPatientListModel
                {
                    FacilityId = patient.FacilityId,
                    PatientId = patient.PatientId,
                    DischargeDate = patient.DischargeDate,
                    IsDischarged = patient.IsDischarged,
                }, cancellationToken);

                patientsModified.Add(patient);
            }
        }

        var eventList = new List<PatientEventResponse>();

        foreach (var patient in patientsModified)
        {
            if (patient.IsDischarged)
            {
                var correlationId = Guid.NewGuid().ToString();
                eventList.Add(new PatientEventResponse
                {
                    CorrelationId = correlationId,
                    FacilityId = request.FacilityId,
                    PatientEvent = new PatientEvent
                    {
                        EventType = PatientEvents.Discharge.ToString(),
                        PatientId = patient.PatientId,
                        ReportTrackingId = request.Message.ReportTrackingId
                    },
                    TopicName = KafkaTopic.PatientEvent.ToString()
                });

                _metrics.IncrementPatientDischargedCounter([
                    new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, request.FacilityId),
                    new KeyValuePair<string, object?>(DiagnosticNames.PatientId, patient.PatientId),
                    new KeyValuePair<string, object?>(DiagnosticNames.PatientEvent, PatientEvents.Discharge.ToString()),
                    new KeyValuePair<string, object?>(DiagnosticNames.CorrelationId, correlationId)
                ]);
            }
        }

        await _historyManager.AddAsync(new PatientCensusHistoricEntity
        {
            CensusDateTime = DateTime.UtcNow,
            CreateDate = DateTime.UtcNow,
            FacilityId = request.FacilityId
        }, cancellationToken);

        return eventList;
    }

    private List<CensusPatientListEntity> ConvertFhirListToEntityList(List fhirList, string facilityId)
    {
        var censusList = new List<CensusPatientListEntity>();
        if (fhirList == null) { return censusList; }

        fhirList.Entry.ForEach(x =>
        {
            var patientId = x.Item.ReferenceElement.Value.SplitReference().Trim();
            censusList.Add(new CensusPatientListEntity
            {
                FacilityId = facilityId,
                PatientId = patientId,
            });
        });
        return censusList;
    }
}
