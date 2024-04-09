using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Report.Repositories
{
    public class MeasureReportConfigRepository : MongoDbRepository<MeasureReportConfigModel>
    {

        private readonly ILogger<MeasureReportConfigRepository> _logger;


        public MeasureReportConfigRepository(IOptions<MongoConnection> mongoSettings, ILogger<MeasureReportConfigRepository> logger) : base(mongoSettings, logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

    }
}
