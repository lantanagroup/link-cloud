using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.Submission.Domain
{
    public class SubmissionEntityRepository<T> : EntityRepository<T> where T : BaseEntity
    {

        public SubmissionEntityRepository(ILogger<SubmissionEntityRepository<T>> logger, TenantSubmissionDbContext dbContext) : base(logger, dbContext)
        {

        }

    }
}
