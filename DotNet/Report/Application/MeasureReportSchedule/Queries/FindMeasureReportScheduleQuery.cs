using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;
using MongoDB.Driver;

namespace LantanaGroup.Link.Report.Application.MeasureReportSchedule.Queries
{
    public class FindMeasureReportScheduleQuery : IRequest<MeasureReportScheduleModel>
    {
        public string Id { get; set; } = string.Empty;
        public string FacilityId { get; set; } = string.Empty;
        public DateTime ReportStartDate { get; set; }
        public DateTime ReportEndDate { get; set; }
    }

    public class FindReportScheduleQueryHandler : IRequestHandler<FindMeasureReportScheduleQuery, MeasureReportScheduleModel>
    {
        private readonly ILogger<FindReportScheduleQueryHandler> _logger;
        private readonly MeasureReportScheduleRepository _repository;

        public FindReportScheduleQueryHandler(ILogger<FindReportScheduleQueryHandler> logger, MeasureReportScheduleRepository repository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<MeasureReportScheduleModel> Handle(FindMeasureReportScheduleQuery request, CancellationToken cancellationToken)
        {
            var res = !string.IsNullOrWhiteSpace(request.Id) ? await _repository.GetAsync(request.Id)
                                    : (await _repository.FindAsync(x => x.FacilityId == request.FacilityId && x.ReportStartDate == request.ReportStartDate && x.ReportEndDate == request.ReportEndDate)).FirstOrDefault();
            return res;
        }
    }
}