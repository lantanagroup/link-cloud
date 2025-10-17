using Reddoxx.Quartz.MongoDbJobStore.Database;
using MongoDB.Driver;
using Microsoft.Extensions.Options;
using LantanaGroup.Link.Shared.Application.Models.Configs;

namespace LantanaGroup.Link.Report.Jobs.JobStoreFactories
{
    // Direct implementation of the Reddoxx interface
    public class ReportQuartzMongoDbJobStoreFactory : IQuartzMongoDbJobStoreFactory
    {
        private readonly IMongoDatabase _database;

        public ReportQuartzMongoDbJobStoreFactory(IOptions<MongoConnection> mongoOptions)
        {
            var options = mongoOptions.Value;
            var client = new MongoClient(options.ConnectionString);
            _database = client.GetDatabase(options.DatabaseName);
        }

        IMongoDatabase IQuartzMongoDbJobStoreFactory.GetDatabase()
        {
            return _database;
        }
    }
}
