using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Report.Repositories
{
    public class MeasureReportSubmissionEntryRepository : MongoDbRepository<MeasureReportSubmissionEntryModel>
    {

        private readonly ILogger<MeasureReportSubmissionEntryRepository> _logger;


        public MeasureReportSubmissionEntryRepository(IOptions<MongoConnection> mongoSettings, ILogger<MeasureReportSubmissionEntryRepository> logger) : base(mongoSettings, logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

    }
}
