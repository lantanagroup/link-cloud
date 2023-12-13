using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;
using MongoDB.Driver;

namespace LantanaGroup.Link.Report.Application.MeasureReportSchedule.Queries
{
    public class GetMeasureReportScheduleForReportIdQuery : IRequest<MeasureReportScheduleModel>
    {
        public string ReportId { get; set; } = string.Empty;
    }

    public class GetMeasureReportScheduleForReportIdQueryHandler : IRequestHandler<GetMeasureReportScheduleForReportIdQuery, MeasureReportScheduleModel>
    {
        private readonly ILogger<FindReportScheduleQueryHandler> _logger;
        private readonly MeasureReportScheduleRepository _repository;

        public GetMeasureReportScheduleForReportIdQueryHandler(ILogger<FindReportScheduleQueryHandler> logger, MeasureReportScheduleRepository repository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<MeasureReportScheduleModel> Handle(GetMeasureReportScheduleForReportIdQuery request, CancellationToken cancellationToken)
        {
            return await _repository.GetAsync(request.ReportId, cancellationToken);
        }
    }
}