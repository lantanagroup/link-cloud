using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;
using MongoDB.Driver;

namespace LantanaGroup.Link.Report.Application.MeasureReportConfig.Queries
{
    public class GetFacilityMeasureReportConfigsQuery : IRequest<IEnumerable<MeasureReportConfigModel>>
    {
        public string FacilityId { get; set; } = string.Empty;
    }

    public class GetFacilityMeasureReportConfigsQueryHandler : IRequestHandler<GetFacilityMeasureReportConfigsQuery, IEnumerable<MeasureReportConfigModel>>
    {
        private readonly ILogger<GetFacilityMeasureReportConfigsQueryHandler> _logger;
        private readonly MeasureReportConfigRepository _repository;

        public GetFacilityMeasureReportConfigsQueryHandler(ILogger<GetFacilityMeasureReportConfigsQueryHandler> logger, MeasureReportConfigRepository repository)
        {
            _logger = logger;
            _repository = repository;
        }

        public async Task<IEnumerable<MeasureReportConfigModel>> Handle(GetFacilityMeasureReportConfigsQuery request, CancellationToken cancellationToken)
        {
            var res = await _repository.FindAsync(x => x.FacilityId == request.FacilityId);
            return res.ToList();
        }
    }
}
