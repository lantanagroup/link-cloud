using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;

namespace LantanaGroup.Link.Normalization.Application.Services;

public class NormalizationEntityRepository<T> : BaseEntityRepository<T, NormalizationDbContext> where T : BaseEntity
{
    public NormalizationEntityRepository(ILogger<NormalizationEntityRepository<T>> logger, NormalizationDbContext dbContext) : base(logger, dbContext)
    {

    }
}