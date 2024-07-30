using Hl7.Fhir.Model;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Messages;
using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using MediatR;
using Microsoft.Data.SqlClient;

namespace LantanaGroup.Link.Census.Application.Commands;

public class ConsumePatientIdsAcquiredEventCommand : IRequest<IEnumerable<BaseResponse>>
{
    public string FacilityId { get; set; }
    public PatientIDsAcquired Message { get; set; }
}

public class ConsumePaitentIdsAcquiredEventHandler : IRequestHandler<ConsumePatientIdsAcquiredEventCommand, IEnumerable<BaseResponse>>
{
    private readonly ILogger<ConsumePaitentIdsAcquiredEventHandler> _logger;
    private readonly ICensusPatientListRepository _patientListRepository;
    private readonly ICensusHistoryRepository _historyRepository;
    private readonly ICensusServiceMetrics _metrics;

    public ConsumePaitentIdsAcquiredEventHandler(
        ILogger<ConsumePaitentIdsAcquiredEventHandler> logger,
        ICensusPatientListRepository patientListRepository,
        ICensusHistoryRepository historyRepository,
        ICensusServiceMetrics metrics)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _patientListRepository = patientListRepository ?? throw new ArgumentNullException(nameof(patientListRepository));
        _historyRepository = historyRepository ?? throw new ArgumentNullException(nameof(historyRepository));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    public async Task<IEnumerable<BaseResponse>> Handle(ConsumePatientIdsAcquiredEventCommand request, CancellationToken cancellationToken)
    {
        /// 1. convert Fhir List to patient entities
        /// 2. get existing census from database
        /// 3. compare:
        ///    - new patients need admitted date updated to DateTime.UtcNow
        ///    - patients that are on existing list and not in new list need to have discharged date set to DateTime.UtcNow
        /// 4. save updated/new patients   
        var convertedList = ConvertFhirListToEntityList(request.Message.PatientIds, request.FacilityId);

        List<CensusPatientListEntity>? activePatients = null;
        try
        {
            activePatients = await _patientListRepository.GetActivePatientsForFacility(request.FacilityId, cancellationToken);
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

        var patientUpdates = new List<CensusPatientListEntity>();

        //find new patients
        foreach (var patient in convertedList)
        {
            if (!activePatients.Any(x => x.PatientId == patient.PatientId))
            {
                var existingPatient = await _patientListRepository.GetPatientByPatientId(request.FacilityId, patient.PatientId, cancellationToken);
                if (existingPatient != null)
                {
                    existingPatient.AdmitDate = DateTime.UtcNow;
                    existingPatient.IsDischarged = false;
                    existingPatient.DischargeDate = null;
                    existingPatient.ModifyDate = DateTime.UtcNow;
                    patientUpdates.Add(existingPatient);
                }
                else
                {
                    patient.AdmitDate = DateTime.UtcNow;
                    patient.CreateDate = DateTime.UtcNow;
                    patient.ModifyDate = DateTime.UtcNow;
                    patientUpdates.Add(patient);
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
                patientUpdates.Add(patient);                
            }
        }

        var eventList = new List<PatientEventResponse>();

        foreach (var patient in patientUpdates)
        {
            await _patientListRepository.UpdateAsync(patient, cancellationToken);
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

        await _historyRepository.AddAsync(new PatientCensusHistoricEntity
        {
            CensusDateTime = DateTime.UtcNow,
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
            var patientId = x.Item.ReferenceElement.Value.Replace("Patient/", "", StringComparison.InvariantCultureIgnoreCase).TrimStart();
            censusList.Add(new CensusPatientListEntity
            {
                FacilityId = facilityId,
                PatientId = patientId,
            });
        });
        return censusList;
    }
}
