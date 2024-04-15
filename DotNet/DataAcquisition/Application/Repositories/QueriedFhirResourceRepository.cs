using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.DataAcquisition.Application.Repositories;

public class QueriedFhirResourceRepository : MongoDbRepository<QueriedFhirResourceRecord>, IQueriedFhirResourceRepository
{
    public QueriedFhirResourceRepository(IOptions<MongoConnection> mongoSettings, ILogger<MongoDbRepository<QueriedFhirResourceRecord>> logger = null) : base(mongoSettings, logger)
    {
    }
}
