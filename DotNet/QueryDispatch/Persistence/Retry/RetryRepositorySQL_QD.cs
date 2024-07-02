using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using QueryDispatch.Domain.Context;

namespace QueryDispatch.Persistence.Retry;

public class RetryRepositorySQL_QD : RetryRepositorySQL
{
    private readonly ILogger<RetryRepositorySQL_QD> _logger;
    private readonly QueryDispatchDbContext _dbContext;

    public RetryRepositorySQL_QD(ILogger<RetryRepositorySQL_QD> logger, QueryDispatchDbContext dbContext) : base(logger, dbContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }
}
