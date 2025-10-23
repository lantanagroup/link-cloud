using MongoDB.Driver;
using Microsoft.Extensions.Options;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Reddoxx.Quartz.MongoDbJobStore.Database;

namespace LantanaGroup.Link.Report.Jobs.JobStoreFactories;

// Implement the REDDOXX interface, not a custom one
public class ReportQuartzMongoDbJobStoreFactory : IQuartzMongoDbJobStoreFactory
{
    private readonly IMongoDatabase _database;

    public ReportQuartzMongoDbJobStoreFactory(IOptions<MongoConnection> mongoOptions)
    {
        var options = mongoOptions.Value;
        var client = new MongoClient(options.ConnectionString);
        _database = client.GetDatabase(options.DatabaseName);
    }

    public IMongoDatabase GetDatabase()  // Make it public, not explicit interface implementation
    {
        return _database;
    }
}