using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;
using MongoDB.Driver;

namespace LantanaGroup.Link.Report.Application.MeasureReportConfig.Queries
{
    public class SearchMeasureReportConfigQuery : IRequest<IEnumerable<MeasureReportConfigModel>>
    {
        public string? FacilityId { get; set; }
        public string? ReportType { get; set; }
        public BundlingType? BundlingType { get; set; }
    }

    public class SearchMeasureReportConfigQueryHandler : IRequestHandler<SearchMeasureReportConfigQuery, IEnumerable<MeasureReportConfigModel>>
    {
        private readonly ILogger<SearchMeasureReportConfigQueryHandler> _logger;
        private readonly MeasureReportConfigRepository _repository;

        public SearchMeasureReportConfigQueryHandler(ILogger<SearchMeasureReportConfigQueryHandler> logger, MeasureReportConfigRepository repository)
        {
            _logger = logger;
            _repository = repository;
        }

        public async Task<IEnumerable<MeasureReportConfigModel>> Handle(SearchMeasureReportConfigQuery request, CancellationToken cancellationToken)
        {
            var res = await _repository.FindAsync(x => 
                x.FacilityId == request.FacilityId
                && (string.IsNullOrWhiteSpace(request.ReportType) || x.ReportType == request.ReportType)
                && (!request.BundlingType.HasValue || x.BundlingType == request.BundlingType)
            );
            return res.ToList();
        }
    }
}
