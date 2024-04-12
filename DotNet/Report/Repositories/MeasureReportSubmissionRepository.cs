using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Report.Repositories
{
    public class MeasureReportSubmissionRepository : MongoDbRepository<MeasureReportSubmissionModel>
    {

        private readonly ILogger<MeasureReportSubmissionRepository> _logger;


        public MeasureReportSubmissionRepository(IOptions<MongoConnection> mongoSettings, ILogger<MeasureReportSubmissionRepository> logger) : base(mongoSettings, logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

    }
}
