using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;
using MongoDB.Driver;

namespace LantanaGroup.Link.Report.Application.MeasureReportSchedule.Queries
{
    public class GetMeasureReportScheduleQuery : IRequest<MeasureReportScheduleModel>
    {
        public string Id { get; set; } = string.Empty;
    }

    public class GetReportScheduleQueryHandler : IRequestHandler<GetMeasureReportScheduleQuery, MeasureReportScheduleModel>
    {
        private readonly ILogger<GetReportScheduleQueryHandler> _logger;
        private readonly MeasureReportScheduleRepository _repository;

        public GetReportScheduleQueryHandler(ILogger<GetReportScheduleQueryHandler> logger, MeasureReportScheduleRepository repository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<MeasureReportScheduleModel> Handle(GetMeasureReportScheduleQuery request, CancellationToken cancellationToken)
        {
            return await _repository.GetAsync(request.Id);
        }
    }
}
