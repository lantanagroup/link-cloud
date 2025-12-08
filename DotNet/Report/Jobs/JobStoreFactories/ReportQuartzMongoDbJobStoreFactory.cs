using LantanaGroup.Link.Report.Domain;
using MongoDB.Driver;
using Reddoxx.Quartz.MongoDbJobStore.Database;

namespace LantanaGroup.Link.Report.Jobs.JobStoreFactories;

// Implement the REDDOXX interface, not a custom one
public class ReportQuartzMongoDbJobStoreFactory : IQuartzMongoDbJobStoreFactory
{
    private readonly MongoDbContext _mongoDbContext;
    private readonly IMongoDatabase _database;

    public ReportQuartzMongoDbJobStoreFactory(MongoDbContext context)
    {
        _mongoDbContext = context;
    }

    public IMongoDatabase GetDatabase()  // Make it public, not explicit interface implementation
    {
        return _mongoDbContext.MongoDatabase;
    }
}