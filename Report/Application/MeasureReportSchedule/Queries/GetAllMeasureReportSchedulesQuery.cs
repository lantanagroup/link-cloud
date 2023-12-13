using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;
using MongoDB.Driver;

namespace LantanaGroup.Link.Report.Application.MeasureReportSchedule.Queries
{
    public class GetAllMeasureReportSchedulesQuery : IRequest<IEnumerable<MeasureReportScheduleModel>>
    {
    }

    public class GetAllMeasureReportSchedulesQueryHandler : IRequestHandler<GetAllMeasureReportSchedulesQuery, IEnumerable<MeasureReportScheduleModel>>
    {
        private readonly ILogger<GetAllMeasureReportSchedulesQueryHandler> _logger;
        private readonly MeasureReportScheduleRepository _repository;

        public GetAllMeasureReportSchedulesQueryHandler(ILogger<GetAllMeasureReportSchedulesQueryHandler> logger, MeasureReportScheduleRepository repository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<IEnumerable<MeasureReportScheduleModel>> Handle(GetAllMeasureReportSchedulesQuery request, CancellationToken cancellationToken)
        {
            return (await _repository.FindAsync(_ => true)).ToEnumerable();
        }
    }
}
