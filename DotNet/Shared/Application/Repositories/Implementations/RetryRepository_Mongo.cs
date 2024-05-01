using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Shared.Application.Repositories.Implementations
{
    public class RetryRepository_Mongo : MongoDbRepository<RetryEntity>, IRetryRepository
    {
        private readonly ILogger<RetryRepository_Mongo> _logger;

        public RetryRepository_Mongo(IOptions<MongoConnection> mongoSettings, ILogger<RetryRepository_Mongo> logger) : base(mongoSettings, logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
    }
}
