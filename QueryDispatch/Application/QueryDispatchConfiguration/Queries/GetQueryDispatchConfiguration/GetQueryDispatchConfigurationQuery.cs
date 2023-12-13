using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Application.ScheduledReport.Queries;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;

namespace LantanaGroup.Link.QueryDispatch.Application.Queries
{
    public class GetQueryDispatchConfigurationQuery : IGetQueryDispatchConfigurationQuery
    {
        private readonly ILogger<GetQueryDispatchConfigurationQuery> _logger;
        private readonly IQueryDispatchConfigurationRepository _dataStore;

        public GetQueryDispatchConfigurationQuery(ILogger<GetQueryDispatchConfigurationQuery> logger, IQueryDispatchConfigurationRepository dataStore) 
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        }

        public async Task<QueryDispatchConfigurationEntity> Execute(string facilityId)
        {
            try
            {
                var config = await _dataStore.GetByFacilityId(facilityId);

                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get query dispatch configuration with a facility id of {facilityId}.", ex);
                throw new ApplicationException($"Failed to query dispatch configuration with a facility id of {facilityId}.");
            }
        }
    }
}
