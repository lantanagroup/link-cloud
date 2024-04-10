using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;
using MongoDB.Driver;

namespace LantanaGroup.Link.Report.Application.MeasureReportSubmission.Queries
{
    public class FindMeasureReportSubmissionByScheduleQuery : IRequest<MeasureReportSubmissionModel>
    {
        public string MeasureReportScheduleId { get; set; } = string.Empty;
    }

    public class FindMeasureReportSubmissionQueryHandler : IRequestHandler<FindMeasureReportSubmissionByScheduleQuery, MeasureReportSubmissionModel>
    {
        private readonly ILogger<FindMeasureReportSubmissionQueryHandler> _logger;
        private readonly MeasureReportSubmissionRepository _repository;

        public FindMeasureReportSubmissionQueryHandler(ILogger<FindMeasureReportSubmissionQueryHandler> logger, MeasureReportSubmissionRepository repository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<MeasureReportSubmissionModel> Handle(FindMeasureReportSubmissionByScheduleQuery request, CancellationToken cancellationToken)
        {
            return (await _repository.FindAsync(x => x.MeasureReportScheduleId == request.MeasureReportScheduleId)).FirstOrDefault();
        }
    }
}
