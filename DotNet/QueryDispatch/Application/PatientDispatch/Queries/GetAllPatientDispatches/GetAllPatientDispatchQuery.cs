using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;

namespace LantanaGroup.Link.QueryDispatch.Application.Queries
{
    public class GetAllPatientDispatchQuery : IGetAllPatientDispatchQuery
    {
        private readonly ILogger<GetAllPatientDispatchQuery> _logger;
        private readonly IPatientDispatchRepository _dataStore;
        public GetAllPatientDispatchQuery(ILogger<GetAllPatientDispatchQuery> logger, IPatientDispatchRepository dataStore)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        }

        public async Task<List<PatientDispatchEntity>> Execute()
        {
            try
            {
                var patientDispatches = await _dataStore.GetAllAsync();
                return patientDispatches.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get all patient dispatches.", ex);
                throw new ApplicationException($"Failed to query all patient dispatches.");
            }
        }
    }
}
