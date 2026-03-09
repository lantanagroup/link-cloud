using LantanaGroup.Link.Report.Domain;
using MongoDB.Driver;
using Reddoxx.Quartz.MongoDbJobStore.Database;

namespace LantanaGroup.Link.Report.Jobs.JobStoreFactories;

public class ReportQuartzMongoDbJobStoreFactory : IQuartzMongoDbJobStoreFactory
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    public ReportQuartzMongoDbJobStoreFactory(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    public IMongoDatabase GetDatabase()
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var mongoDbContext = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
        return mongoDbContext.MongoDatabase;
    }
}