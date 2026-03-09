using LantanaGroup.Link.Report.Domain;
using MongoDB.Driver;
using Reddoxx.Quartz.MongoDbJobStore.Database;

namespace LantanaGroup.Link.Report.Jobs.JobStoreFactories;

public class ReportQuartzMongoDbJobStoreFactory : IQuartzMongoDbJobStoreFactory
{
    private readonly MongoDbContext _mongoDbContext;

    public ReportQuartzMongoDbJobStoreFactory(IServiceScopeFactory serviceScopeFactory)
    {
        using var scope = serviceScopeFactory.CreateScope();
        _mongoDbContext = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
    }

    public IMongoDatabase GetDatabase()
    {
        return _mongoDbContext.MongoDatabase;
    }
}