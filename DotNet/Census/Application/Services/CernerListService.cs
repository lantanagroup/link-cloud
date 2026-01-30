using Hl7.Fhir.Model;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Application.Models.Payloads.Cerner;
using LantanaGroup.Link.Census.Application.Models.Payloads.CernerList;
using LantanaGroup.Link.Census.Application.Models.Payloads.Fhir.List;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Census.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.DataAcq;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.Census.Application.Services
{
    public interface ICernerListService 
    {
        Task<List<IBaseResponse>> ProcessList(string facilityId, CernerPatientsAcquired cernerEventValue, CancellationToken cancellationToken);
        Task<List<IBaseResponse>> ProcessDischarges(string facilityId, CernerPatientsAcquired cernerEventValue, CancellationToken cancellationToken);
    }
    public class CernerListService : ICernerListService
    {
        private readonly ILogger<PatientListService> _logger;
        private readonly ICensusServiceMetrics _metrics;
        private readonly IPatientEventManager _patientEventManager;
        private readonly IPatientEventQueries _patientEventQueries;
        private readonly IPatientEncounterQueries _patientEncounterQueries;
        private readonly IPatientEncounterManager _patientEncounterManager;
        private readonly ICensusConfigManager _censusConfigManager;

        public CernerListService(ILogger<PatientListService> logger, ICensusServiceMetrics metrics, IPatientEventManager patientEventManager, IPatientEventQueries patientEventQueries, IPatientEncounterQueries patientEncounterQueries, IPatientEncounterManager patientEncounterManager, ICensusConfigManager censusConfigManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _patientEventQueries = patientEventQueries ?? throw new ArgumentNullException(nameof(patientEventQueries));
            _patientEventManager = patientEventManager ?? throw new ArgumentNullException(nameof(patientEventManager));
            _patientEncounterQueries =
                patientEncounterQueries ?? throw new ArgumentNullException(nameof(patientEncounterQueries));
            _patientEncounterManager =
                patientEncounterManager ?? throw new ArgumentNullException(nameof(patientEncounterManager));
            _censusConfigManager = censusConfigManager ?? throw new ArgumentNullException(nameof(censusConfigManager));
        }

        public async Task<List<IBaseResponse>> ProcessDischarges(string facilityId, CernerPatientsAcquired cernerEventValue, CancellationToken cancellationToken)
        {
            var admittedPatients = await _patientEncounterQueries.GetAdmittedPatientsByFacility(facilityId, cancellationToken);

            if (admittedPatients.Count() == 0) {
                return new List<IBaseResponse>();
            }

            await using var transaction = await _patientEventQueries.StartTransaction(cancellationToken);

            foreach (var admitPatient in admittedPatients) 
            {
                string admitPatientIdentifier = admitPatient.GetIdentifierByType(SourceType.SFTP).Identifier;

                if (cernerEventValue.PatientEncounters.Any(x => x.PatientId == admitPatientIdentifier)) 
                {
                    continue;
                }

                var dischargePayload = new CernerListDischargePayload(admitPatientIdentifier, DateTime.UtcNow);
                var patientEvent = dischargePayload.CreatePatientEvent(facilityId, admitPatient.CorrelationId);

                dischargePayload.UpdatePatientEncounter(admitPatient);
                
                await _patientEventManager.AddPatientEvent(patientEvent, cancellationToken);
                await _patientEncounterManager.UpdatePatientEncounterAsync(admitPatient, cancellationToken);
                await _patientEventQueries.CommitTransaction(transaction, cancellationToken);
            }

            //TODO: Daniel - Fix 
            return new List<IBaseResponse>();
        }
        public async Task<List<IBaseResponse>> ProcessList(string facilityId, CernerPatientsAcquired cernerEventValue, CancellationToken cancellationToken)
        {
            List<IBaseResponse> messages = new List<IBaseResponse>();
            var admittedPatients = await _patientEncounterQueries.GetAdmittedPatientsByFacility(facilityId, cancellationToken);

            foreach (var eventEncounter in cernerEventValue.PatientEncounters)
            {
                await using var transaction = await _patientEventQueries.StartTransaction(cancellationToken);
                try
                {
                    //var latestEvent = await _patientEventQueries.GetLatestEventByFacilityAndPatientId(facilityId, eventEncounter.PatientId, cancellationToken);

                    var foundPatientEncounter = admittedPatients.FirstOrDefault(
                        x => x.MedicalRecordNumber == eventEncounter.MRN && 
                        x.PatientIdentifiers.Any(
                            y => y.Identifier == eventEncounter.PatientId && 
                            y.SourceType == SourceType.SFTP.ToString()));

                    if (foundPatientEncounter != null && foundPatientEncounter.EncounterStatus == eventEncounter.EncounterStatus && foundPatientEncounter.EncounterType == eventEncounter.EncounterType && foundPatientEncounter.MedicalRecordNumber == eventEncounter.MRN) 
                    {
                        continue;
                    }

                    if (foundPatientEncounter != null)
                    {
                        var updatePayload = new CernerListUpdatePayload(eventEncounter.PatientId, eventEncounter.EncounterId, eventEncounter.FinNumber, eventEncounter.MRN, eventEncounter.EncounterStatus, eventEncounter.EncounterType);
                        var patientEvent = updatePayload.CreatePatientEvent(facilityId, foundPatientEncounter.CorrelationId);

                        updatePayload.UpdatePatientEncounter(foundPatientEncounter);

                        await _patientEventManager.AddPatientEvent(patientEvent, cancellationToken);
                        await _patientEncounterManager.UpdatePatientEncounterAsync(foundPatientEncounter, cancellationToken);
                    }
                    else 
                    {
                        var correlationId = Guid.NewGuid().ToString();
                        var admitPayload = new CernerListAdmitPayload(eventEncounter.PatientId, DateTime.UtcNow, eventEncounter.EncounterId, eventEncounter.FinNumber, eventEncounter.MRN, eventEncounter.EncounterStatus, eventEncounter.EncounterType);


                        var patientEvent = admitPayload.CreatePatientEvent(facilityId, correlationId);

                        await _patientEventManager.AddPatientEvent(patientEvent, cancellationToken);

                        PatientEncounter encounter = await _patientEncounterQueries.GetPatientEncounterByCorrelationIdAsync(correlationId, cancellationToken);

                        if (encounter == null)
                        {
                            var patientEncounter = admitPayload.CreatePatientEncounter(facilityId, correlationId);
                            encounter = await _patientEncounterManager.AddPatientEncounterAsync(patientEncounter,
                                cancellationToken);
                        }
                    }

                    await _patientEventQueries.CommitTransaction(transaction, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing patient list for facility {FacilityId}", facilityId);
                    await _patientEventQueries.RollbackTransaction(transaction, cancellationToken);
                    throw;
                }
            }

            return messages;
        }
    }
}
