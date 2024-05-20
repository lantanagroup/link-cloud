using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using QueryDispatch.Domain.Context;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.QueryDispatch.Persistence.PatientDispatch
{
    public class PatientDispatchRepo : BaseSqlConfigurationRepo<PatientDispatchEntity>, IPatientDispatchRepository
    {
        private readonly ILogger<PatientDispatchRepo> _logger;
        private readonly QueryDispatchDbContext _dbContext;

        public PatientDispatchRepo(ILogger<PatientDispatchRepo> logger, QueryDispatchDbContext dbContext) : base(logger, dbContext)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<bool> Delete(string facilityId, string patientId)
        {
            try
            {
                var entity = await _dbContext.PatientDispatches.FirstOrDefaultAsync(x => x.FacilityId == facilityId && x.PatientId == patientId);
                if (entity != null)
                {
                    _dbContext.PatientDispatches.Remove(entity);
                    await _dbContext.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Patient dispatch delete exception for patientId {patientId} in facility {facilityId}", patientId, facilityId);

                return false;
            }
        }

        public Task<List<PatientDispatchEntity>> GetAllAsync()
        {
            return _dbContext.PatientDispatches.ToListAsync();
        }
    }
}
