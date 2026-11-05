using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.QueryDispatch.Presentation.Services;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Quartz;

namespace QueryDispatch.Domain.Managers
{

    public interface IPatientDispatchManager
    {
        public Task<string> createPatientDispatch(PatientDispatchEntity patientDispatch, CancellationToken cancellationToken = default);
        public Task<bool> deletePatientDispatch(string facilityId, string patientId);
    }

    public class PatientDispatchManager : IPatientDispatchManager
    {
        private readonly IBaseEntityRepository<PatientDispatchEntity> _repository;
        private readonly ILogger<QueryDispatchConfigurationManager> _logger;
        private readonly ISchedulerFactory _schedulerFactory;

        public PatientDispatchManager(ILogger<QueryDispatchConfigurationManager> logger, IDatabase database, ISchedulerFactory schedulerFactory)
        {
            _repository = database.PatientDispatchRepo;
            _logger = logger;
            _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
        }



        public async Task<string> createPatientDispatch(PatientDispatchEntity patientDispatch, CancellationToken cancellationToken = default)
        {
            try
            {
                //_datastore.Add(patientDispatch);
                await _repository.AddAsync(patientDispatch, cancellationToken);

                _logger.LogInformation("Created patient dispatch for patient id {PatientId} in facility {FacilityId}", HtmlInputSanitizer.Sanitize(patientDispatch.PatientId), HtmlInputSanitizer.Sanitize(patientDispatch.FacilityId));

                await ScheduleService.CreateJobAndTrigger(patientDispatch, await _schedulerFactory.GetScheduler(cancellationToken), cancellationToken);

                return patientDispatch.FacilityId;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create patient dispatch exception for patient id {PatientId} in facility {FacilityId}", HtmlInputSanitizer.Sanitize(patientDispatch.PatientId), HtmlInputSanitizer.Sanitize(patientDispatch.FacilityId));
                throw new ApplicationException($"Failed to create patient dispatch record for patient id {HtmlInputSanitizer.Sanitize(patientDispatch.PatientId)} in facility {HtmlInputSanitizer.Sanitize(patientDispatch.FacilityId)}.");
            }
        }

        public async Task<bool> deletePatientDispatch(string facilityId, string patientId)
        {
            try
            {
                var entity = await _repository.FirstOrDefaultAsync(x => x.FacilityId == facilityId && x.PatientId == patientId);
                if (entity != null)
                {
                    await _repository.RemoveAsync(entity);
                }
                _logger.LogInformation("Deleted Patient Dispatch record for patient id {PatientId} in facility {FacilityId}", HtmlInputSanitizer.Sanitize(patientId), HtmlInputSanitizer.Sanitize(facilityId));

                return true;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Patient dispatch delete exception for patientId {patientId} in facility {facilityId}", HtmlInputSanitizer.Sanitize(patientId), HtmlInputSanitizer.Sanitize(facilityId));

                return false;
            }
        }
    }
}
