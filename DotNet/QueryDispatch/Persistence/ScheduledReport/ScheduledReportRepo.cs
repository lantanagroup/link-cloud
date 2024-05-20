using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using QueryDispatch.Domain.Context;

namespace LantanaGroup.Link.QueryDispatch.Persistence.ScheduledReport
{
    public class ScheduledReportRepo : BaseSqlConfigurationRepo<ScheduledReportEntity>, IScheduledReportRepository
    {
        private readonly ILogger<ScheduledReportRepo> _logger;
        private readonly QueryDispatchDbContext _dbContext;

        public ScheduledReportRepo(ILogger<ScheduledReportRepo> logger, QueryDispatchDbContext dbContext) : base(logger, dbContext) 
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public ScheduledReportEntity GetByFacilityId(string facilityId)
        {
            try
            {
                var entity = _dbContext.ScheduledReports.FirstOrDefault(x => x.FacilityId == facilityId);
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to execute get ScheduledReportEntity by id {facilityId}.", ex);
                throw new ApplicationException($"Failed to execute the request to get the specified ScheduledReportEntity.");
            }
        }

        public async Task Update(ScheduledReportEntity scheduledReport)
        {
            try
            {
                _dbContext.ScheduledReports.Update(scheduledReport);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Update exception: {ex.Message}");
            }
        }
    }
}
