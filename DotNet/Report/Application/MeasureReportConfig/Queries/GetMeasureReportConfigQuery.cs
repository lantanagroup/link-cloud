using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;
using MongoDB.Driver;

namespace LantanaGroup.Link.Report.Application.MeasureReportConfig.Queries
{
    public class GetMeasureReportConfigQuery : IRequest<MeasureReportConfigModel>
    {
        public string Id { get; set; } = string.Empty;
    }

    public class GetMeasureReportConfigQueryHandler : IRequestHandler<GetMeasureReportConfigQuery, MeasureReportConfigModel>
    {
        private readonly ILogger<GetMeasureReportConfigQueryHandler> _logger;
        private readonly MeasureReportConfigRepository _repository;

        public GetMeasureReportConfigQueryHandler(ILogger<GetMeasureReportConfigQueryHandler> logger, MeasureReportConfigRepository repository)
        {
            _logger = logger;
            _repository = repository;
        }

        public async Task<MeasureReportConfigModel> Handle(GetMeasureReportConfigQuery request, CancellationToken cancellationToken)
        {
            return await _repository.GetAsync(request.Id);
        }
    }
}
