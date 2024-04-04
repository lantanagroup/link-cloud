using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Shared.Application.Repositories.Implementations
{
    public class RetryRepository : MongoDbRepository<RetryEntity>
    {
        private readonly ILogger<RetryRepository> _logger;

        public RetryRepository(IOptions<MongoConnection> mongoSettings, ILogger<RetryRepository> logger) : base(mongoSettings, logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
    }
}
