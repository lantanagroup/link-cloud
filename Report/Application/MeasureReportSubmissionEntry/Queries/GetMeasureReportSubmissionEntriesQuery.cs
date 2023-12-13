using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;
using MongoDB.Driver;

namespace LantanaGroup.Link.Report.Application.MeasureReportSubmissionEntry.Queries
{
    public class GetMeasureReportSubmissionEntriesQuery : IRequest<IEnumerable<MeasureReportSubmissionEntryModel>>
    {
        public string MeasureReportScheduleId { get; set; } = string.Empty;
    }

    public class GetMeasureReportSubmissionEntriesQueryHandler : IRequestHandler<GetMeasureReportSubmissionEntriesQuery, IEnumerable<MeasureReportSubmissionEntryModel>>
    {
        private readonly ILogger<GetMeasureReportSubmissionEntriesQueryHandler> _logger;
        private readonly MeasureReportSubmissionEntryRepository _repository;

        public GetMeasureReportSubmissionEntriesQueryHandler(ILogger<GetMeasureReportSubmissionEntriesQueryHandler> logger, MeasureReportSubmissionEntryRepository repository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<IEnumerable<MeasureReportSubmissionEntryModel>> Handle(GetMeasureReportSubmissionEntriesQuery request, CancellationToken cancellationToken)
        {
            return (await _repository.FindAsync(x => x.MeasureReportScheduleId == request.MeasureReportScheduleId)).ToEnumerable();
        }
    }
}
