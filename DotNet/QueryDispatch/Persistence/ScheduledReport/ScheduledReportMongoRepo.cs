using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;

namespace LantanaGroup.Link.QueryDispatch.Persistence.ScheduledReport
{
    public class ScheduledReportMongoRepo : BaseMongoRepository<ScheduledReportEntity>, IScheduledReportRepository
    {
        private readonly ILogger<ScheduledReportMongoRepo> _logger;

        public ScheduledReportMongoRepo(IOptions<MongoConnection> mongoSettings, ILogger<ScheduledReportMongoRepo> logger) : base(mongoSettings, logger) 
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public ScheduledReportEntity GetByFacilityId(string facilityId)
        {
            try
            {
                var filter = Builders<ScheduledReportEntity>.Filter.Eq(x => x.FacilityId, facilityId);
                ScheduledReportEntity entity = _collection.Find(filter).FirstOrDefault();
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
                await _collection.ReplaceOneAsync(x => x.Id == scheduledReport.Id, scheduledReport);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Update exception: {ex.Message}");
            }
        }
    }
}
