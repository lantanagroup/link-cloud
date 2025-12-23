using LantanaGroup.Link.Report.Domain;
using MongoDB.Driver;
using Reddoxx.Quartz.MongoDbJobStore.Database;

namespace LantanaGroup.Link.Report.Jobs.JobStoreFactories;

public class ReportQuartzMongoDbJobStoreFactory : IQuartzMongoDbJobStoreFactory
{
    private readonly MongoDbContext _mongoDbContext;

    public ReportQuartzMongoDbJobStoreFactory(MongoDbContext context)
    {
        _mongoDbContext = context;
    }

    public IMongoDatabase GetDatabase()
    {
        return _mongoDbContext.MongoDatabase;
    }
}