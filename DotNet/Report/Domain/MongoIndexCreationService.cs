
using System.Threading;

namespace LantanaGroup.Link.Report.Domain
{
    public class MongoIndexCreationService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        public MongoIndexCreationService(IServiceScopeFactory factory) 
        {
            _serviceScopeFactory = factory;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
            await context.EnsureIndexesAsync(cancellationToken);

            await StopAsync(cancellationToken);
        }
    }
}
