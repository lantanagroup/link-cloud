﻿using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.Shared.Application.Repositories.Implementations;
public class RetryRepository_SQL : BaseSqlConfigurationRepo<RetryEntity>, IRetryRepository 
{
    private readonly ILogger<BaseSqlConfigurationRepo<RetryEntity>> _logger;
    private readonly DbContext _context;
    public RetryRepository_SQL(ILogger<BaseSqlConfigurationRepo<RetryEntity>> logger, DbContext dbContext) : base(logger, dbContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _context = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<List<RetryEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _context.Set<RetryEntity>().ToListAsync(cancellationToken);
    }
}