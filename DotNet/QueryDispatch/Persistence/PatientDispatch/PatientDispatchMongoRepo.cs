using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;

namespace LantanaGroup.Link.QueryDispatch.Persistence.PatientDispatch
{
    public class PatientDispatchMongoRepo : BaseMongoRepository<PatientDispatchEntity>, IPatientDispatchRepository
    {
        private readonly ILogger<PatientDispatchMongoRepo> _logger;

        public PatientDispatchMongoRepo(IOptions<MongoConnection> mongoSettings, ILogger<PatientDispatchMongoRepo> logger) : base(mongoSettings, logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> Delete(string facilityId, string patientId)
        {
            try
            {
                var deleteResult = await _collection.DeleteOneAsync(x => x.FacilityId == facilityId && x.PatientId == patientId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Patient dispatch delete exception for patientId {patientId} in facility {facilityId}: {ex.Message}");

                return false;
            }
        }
    }
}
