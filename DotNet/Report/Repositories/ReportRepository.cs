using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Report.Repositories
{
    public class ReportRepository : MongoDbRepository<ReportModel>
    {

        private readonly ILogger<ReportRepository> _logger;

        public ReportRepository(IOptions<MongoConnection> mongoSettings, ILogger<ReportRepository> logger) : base(mongoSettings, logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

    }
}
