using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace LantanaGroup.Link.DataAcquisition.Application.Repositories;

public class QueriedFhirResourceRepository : BaseSqlConfigurationRepo<QueriedFhirResourceRecord>, IQueriedFhirResourceRepository
{
    private readonly ILogger<QueriedFhirResourceRepository> _logger;
    private readonly DataAcquisitionDbContext _dbContext;

    public QueriedFhirResourceRepository(ILogger<QueriedFhirResourceRepository> logger, DataAcquisitionDbContext dbContext)
        : base(logger, dbContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public void Dispose()
    {
    }

    public async Task<List<QueriedFhirResourceRecord>> GetQueryResultsAsync(string facilityId, string? patientId = default, string? correlationId = default, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(patientId) && !string.IsNullOrWhiteSpace(correlationId))
        {
            return await _dbContext.QueriedFhirResources.Where(x => x.FacilityId == facilityId && x.PatientId == patientId && x.CorrelationId == correlationId).ToListAsync();
        }

        if (!string.IsNullOrWhiteSpace(patientId) && string.IsNullOrWhiteSpace(correlationId))
        {
            return await _dbContext.QueriedFhirResources.Where(x => x.FacilityId == facilityId && x.PatientId == patientId).ToListAsync();
        }

        if (string.IsNullOrWhiteSpace(patientId) && !string.IsNullOrWhiteSpace(correlationId))
        {
            return await _dbContext.QueriedFhirResources.Where(x => x.FacilityId == facilityId && x.CorrelationId == correlationId).ToListAsync();
        }

        _logger.LogWarning("Parameters passed to GetQueryResultsAsync are invalid. Please provide at least one valid parameter.\n facilityId: {1} \n patientId: {2} \n correlationId: {3}", facilityId, patientId, correlationId);
        return null;
    }
}
