using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;
using MongoDB.Driver;

namespace LantanaGroup.Link.Report.Application.MeasureReportConfig.Queries
{
    public class FindMeasureReportConfigQuery : IRequest<MeasureReportConfigModel>
    {
        public string FacilityId { get; set; } = string.Empty;
        public string ReportType { get; set; } = string.Empty;
    }

    public class FindMeasureReportConfigQueryHandler : IRequestHandler<FindMeasureReportConfigQuery, MeasureReportConfigModel>
    {
        private readonly ILogger<FindMeasureReportConfigQueryHandler> _logger;
        private readonly MeasureReportConfigRepository _repository;

        public FindMeasureReportConfigQueryHandler(ILogger<FindMeasureReportConfigQueryHandler> logger, MeasureReportConfigRepository repository)
        {
            _logger = logger;
            _repository = repository;
        }

        public async Task<MeasureReportConfigModel> Handle(FindMeasureReportConfigQuery request, CancellationToken cancellationToken)
        {
            return (await _repository.FindAsync(x => x.FacilityId == request.FacilityId && x.ReportType == request.ReportType)).FirstOrDefault();
        }
    }
}
