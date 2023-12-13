using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;
using MongoDB.Driver;

namespace LantanaGroup.Link.Report.Application.MeasureReportSchedule.Queries
{
    public class FindMeasureReportScheduleForReportTypeQuery : IRequest<MeasureReportScheduleModel>
    {
        public string FacilityId { get; set; } = string.Empty;
        public DateTime ReportStartDate { get; set; }
        public DateTime ReportEndDate { get; set; }
        public string ReportType { get; set; }
    }

    public class FindMeasureReportScheduleForReportTypeQueryHandler : IRequestHandler<FindMeasureReportScheduleForReportTypeQuery, MeasureReportScheduleModel>
    {
        private readonly ILogger<FindMeasureReportScheduleForReportTypeQueryHandler> _logger;
        private readonly MeasureReportScheduleRepository _repository;

        public FindMeasureReportScheduleForReportTypeQueryHandler(ILogger<FindMeasureReportScheduleForReportTypeQueryHandler> logger, MeasureReportScheduleRepository repository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<MeasureReportScheduleModel> Handle(FindMeasureReportScheduleForReportTypeQuery request, CancellationToken cancellationToken)
        {
            var res = await _repository.FindAsync(x => x.FacilityId == request.FacilityId && x.ReportStartDate == request.ReportStartDate && x.ReportEndDate == request.ReportEndDate && x.ReportType == request.ReportType);
            return res.FirstOrDefault();
        }
    }
}
