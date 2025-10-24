using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Results;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using System.Data.Entity;

namespace DataAcquisition.Domain.Application.Queries
{
    public interface IFhirQueryQueries
    {
        Task<FhirQueryResultModel> GetFhirQueriesAsync(string facilityId, string? correlationId = default, string? patientId = default, string? resourceType = default, CancellationToken cancellationToken = default);
    }

    public class FhirQueryQueries : IFhirQueryQueries
    {
        private readonly DataAcquisitionDbContext _dbContext;

        public FhirQueryQueries(DataAcquisitionDbContext dbContext) 
        {
            _dbContext = dbContext;
        }

        public async Task<FhirQueryResultModel> GetFhirQueriesAsync(string facilityId, string? correlationId = null, string? patientId = null, string? resourceType = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                throw new ArgumentNullException(nameof(facilityId));
            }

            var query = from q in _dbContext.FhirQueries
                        where q.FacilityId == facilityId
                        select q;

            if (!string.IsNullOrEmpty(correlationId))
            {
                query = query.Where(x => x.DataAcquisitionLog.CorrelationId == correlationId);
            }

            if (!string.IsNullOrEmpty(patientId))
            {
                query = query.Where(x => x.DataAcquisitionLog.PatientId == patientId);
            }

            if (!string.IsNullOrEmpty(resourceType))
            {
                query = query.Where(x => x.DataAcquisitionLog.FhirQueries.Any(y => y.FhirQueryResourceTypes.Any(z => z.ResourceType.Equals(resourceType))));
            }

            return new FhirQueryResultModel { Queries = await query.ToListAsync() };
        }
    }
}
