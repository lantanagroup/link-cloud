using LantanaGroup.Link.Report.Application.Interfaces;
using Quartz.Impl;
using System.Collections.Specialized;

namespace LantanaGroup.Link.Report.Application.Factory;

public class MongoSchedulerFactory : StdSchedulerFactory, IMongoSchedulerFactory
{
    public MongoSchedulerFactory()
    {
    }

    public MongoSchedulerFactory(NameValueCollection props) : base(props)
    {
    }
}
