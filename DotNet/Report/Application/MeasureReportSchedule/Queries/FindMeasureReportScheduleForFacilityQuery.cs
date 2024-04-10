using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;
using MongoDB.Driver;

namespace LantanaGroup.Link.Report.Application.MeasureReportSchedule.Queries
{
    public class FindMeasureReportScheduleForFacilityQuery : IRequest<List<MeasureReportScheduleModel>>
    {
        public string FacilityId { get; set; } = string.Empty;
    }

    public class FindMeasureReportScheduleForFacilityQueryHandler : IRequestHandler<FindMeasureReportScheduleForFacilityQuery, List<MeasureReportScheduleModel>>
    {
        private readonly ILogger<FindReportScheduleQueryHandler> _logger;
        private readonly MeasureReportScheduleRepository _repository;

        public FindMeasureReportScheduleForFacilityQueryHandler(ILogger<FindReportScheduleQueryHandler> logger, MeasureReportScheduleRepository repository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<List<MeasureReportScheduleModel>> Handle(FindMeasureReportScheduleForFacilityQuery request, CancellationToken cancellationToken)
        {
            return await (await _repository.FindAsync(x => x.FacilityId == request.FacilityId, cancellationToken)).ToListAsync(cancellationToken) ?? new List<MeasureReportScheduleModel>();
        }
    }
}