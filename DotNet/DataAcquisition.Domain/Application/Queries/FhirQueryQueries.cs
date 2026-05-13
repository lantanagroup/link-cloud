using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Results;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

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
            using var activity = ServiceActivitySource.Instance.StartActivity("FhirQueryQueries.GetFhirQueriesAsync");
            activity?.SetTag(DiagnosticNames.FacilityId, facilityId);
            activity?.SetTag(DiagnosticNames.CorrelationId, correlationId);
            activity?.SetTag(DiagnosticNames.PatientId, patientId);
            activity?.SetTag(DiagnosticNames.ResourceType, resourceType);

            if (string.IsNullOrWhiteSpace(facilityId))
            {
                throw new ArgumentNullException(nameof(facilityId));
            }

            var query = _dbContext.FhirQueries.AsNoTracking()
             .Where(q => q.FacilityId == facilityId);

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
