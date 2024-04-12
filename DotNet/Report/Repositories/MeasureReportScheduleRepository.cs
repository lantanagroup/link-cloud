using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Report.Repositories
{
    public class MeasureReportScheduleRepository : MongoDbRepository<MeasureReportScheduleModel>
    {

        private readonly ILogger<MeasureReportScheduleRepository> _logger;

        public MeasureReportScheduleRepository(IOptions<MongoConnection> mongoSettings, ILogger<MeasureReportScheduleRepository> logger) : base(mongoSettings, logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

    }
}
