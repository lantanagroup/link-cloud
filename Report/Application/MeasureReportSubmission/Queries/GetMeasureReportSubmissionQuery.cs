using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;
using MongoDB.Driver;

namespace LantanaGroup.Link.Report.Application.MeasureReportSubmission.Queries
{
    public class GetMeasureReportSubmissionQuery : IRequest<MeasureReportSubmissionModel>
    {
        public string Id { get; set; } = string.Empty;
    }

    public class GetMeasureReportSubmissionQueryHandler : IRequestHandler<GetMeasureReportSubmissionQuery, MeasureReportSubmissionModel>
    {
        private readonly ILogger<GetMeasureReportSubmissionQueryHandler> _logger;
        private readonly MeasureReportSubmissionRepository _repository;

        public GetMeasureReportSubmissionQueryHandler(ILogger<GetMeasureReportSubmissionQueryHandler> logger, MeasureReportSubmissionRepository repository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<MeasureReportSubmissionModel> Handle(GetMeasureReportSubmissionQuery request, CancellationToken cancellationToken)
        {
            return (await _repository.FindAsync(x => x.Id == request.Id)).FirstOrDefault();
        }
    }
}
