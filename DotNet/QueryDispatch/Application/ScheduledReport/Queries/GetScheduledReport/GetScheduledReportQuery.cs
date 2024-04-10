using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;

namespace LantanaGroup.Link.QueryDispatch.Application.ScheduledReport.Queries
{
    public class GetScheduledReportQuery : IGetScheduledReportQuery
    {
        private readonly ILogger<GetScheduledReportQuery> _logger;
        private readonly IScheduledReportRepository _dataStore;

        public GetScheduledReportQuery(ILogger<GetScheduledReportQuery> logger, IScheduledReportRepository dataStore) 
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        }

        public ScheduledReportEntity Execute(string facilityId)
        {
            try
            {
                var result = _dataStore.GetByFacilityId(facilityId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get scheduled reports for facility {facilityId}.", ex);
                throw new ApplicationException($"Failed to get scheduled reports for facility {facilityId}.");
            }
        }
    }
}
