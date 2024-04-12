using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;

namespace LantanaGroup.Link.QueryDispatch.Application.Queries
{
    public class GetAllQueryDispatchConfigurationQuery : IGetAllQueryDispatchConfigurationQuery
    {
        private readonly ILogger<GetQueryDispatchConfigurationQuery> _logger;
        private readonly IQueryDispatchConfigurationRepository _dataStore;
        public GetAllQueryDispatchConfigurationQuery(ILogger<GetQueryDispatchConfigurationQuery> logger, IQueryDispatchConfigurationRepository dataStore)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        }

        public async Task<List<QueryDispatchConfigurationEntity>> Execute()
        {
            try
            {
                var configs = await _dataStore.GetAllAsync();
                return configs.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get all query dispatch configurations.", ex);
                throw new ApplicationException($"Failed to query all dispatch configurations.");
            }
        }
    }
}
