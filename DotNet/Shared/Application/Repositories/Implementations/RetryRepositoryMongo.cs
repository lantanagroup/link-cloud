using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Shared.Application.Repositories.Implementations
{
    public class RetryRepositoryMongo : MongoDbRepository<RetryEntity>
    {
        private readonly ILogger<RetryRepositoryMongo> _logger;

        public RetryRepositoryMongo(IOptions<MongoConnection> mongoSettings, ILogger<RetryRepositoryMongo> logger) : base(mongoSettings, logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
    }
}
