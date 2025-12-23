
using System.Threading;

namespace LantanaGroup.Link.Report.Domain
{
    public class MongoIndexCreationService : BackgroundService
    {
        private readonly ILogger<MongoIndexCreationService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        public MongoIndexCreationService(ILogger<MongoIndexCreationService> logger, IServiceScopeFactory factory) 
        {
            _logger = logger;
            _serviceScopeFactory = factory;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Mongo Index Creation Service is running");
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
            await context.EnsureIndexesAsync(cancellationToken);

            await StopAsync(cancellationToken);
            _logger.LogInformation("Mongo Index Creation Service has completed.");
        }
    }
}
