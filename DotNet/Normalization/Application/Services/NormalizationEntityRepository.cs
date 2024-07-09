using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.Normalization.Application.Services;

public class NormalizationEntityRepository<T> : EntityRepository<T> where T : BaseEntity
{
    public NormalizationEntityRepository(ILogger<NormalizationEntityRepository<T>> logger, NormalizationDbContext dbContext) : base(logger, dbContext)
    {

    }
}