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

    public async Task<List<QueriedFhirResourceRecord>> GetQueryResultsAsync(string correlationId, string queryType)
    {
        var query = _dbContext.QueriedFhirResources.Where(x => x.CorrelationId == correlationId);

        if (!string.IsNullOrWhiteSpace(queryType))
        {
            query.Where(x => x.QueryType == queryType);
        }

        return await query.ToListAsync();
    }
}
