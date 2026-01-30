using Hl7.Fhir.Model;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Application.Models.Payloads.Cerner;
using LantanaGroup.Link.Census.Application.Models.Payloads.Fhir.List;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Census.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.DataAcq;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;

namespace LantanaGroup.Link.Census.Application.Services
{
    public interface ICernerListService 
    {
        Task<List<IBaseResponse>> ProcessList(string facilityId, CernerPatientsAcquired cernerEventValue, CancellationToken cancellationToken);
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

        public async Task<List<IBaseResponse>> ProcessList(string facilityId, CernerPatientsAcquired cernerEventValue, CancellationToken cancellationToken)
        {
            List<IBaseResponse> messages = new List<IBaseResponse>();
            foreach (var encounter in cernerEventValue.PatientEncounters) 
            {
                await using var transaction = await _patientEventQueries.StartTransaction(cancellationToken);

                try
                {
                    var existingEvent = await _patientEventQueries.GetLatestEventByFacilityAndPatientId(facilityId, encounter.PatientId, cancellationToken);

                    var correlationId = Guid.NewGuid().ToString();

                    var admitPayload = new CernerListAdmitPayload(encounter.PatientId, DateTime.UtcNow, encounter.EncounterId, encounter.FinNumber, encounter.MRN, encounter.EncounterStatus, encounter.EncounterType);
                    var patientEvent = admitPayload.CreatePatientEvent(facilityId, correlationId);

                    await _patientEventManager.AddPatientEvent(patientEvent, cancellationToken);

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
