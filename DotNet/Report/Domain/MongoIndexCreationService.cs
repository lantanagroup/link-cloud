
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
            var context = _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<MongoDbContext>();
            await context.EnsureIndexesAsync(cancellationToken);

            await StopAsync(cancellationToken);
        }
    }
}
