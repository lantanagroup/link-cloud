using Reddoxx.Quartz.MongoDbJobStore;
using MongoDB.Driver;
using System;

namespace LantanaGroup.Link.Report.Jobs.JobStoreFactories
{
    public interface IQuartzMongoDbJobStoreFactory
    {
        IMongoDatabase GetDatabase();
    }

    public class QuartzMongoDbJobStoreFactory : IQuartzMongoDbJobStoreFactory
    {
        public IMongoDatabase GetDatabase()
        {
            // TODO: Implement logic to return the MongoDB database instance for Quartz
            throw new NotImplementedException();
        }
    }
}
