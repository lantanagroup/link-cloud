using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;
using MongoDB.Driver;

namespace LantanaGroup.Link.Report.Application.MeasureReportSchedule.Queries
{
    public class GetMeasureReportSchedulesByIsSubmittedQuery : IRequest<IEnumerable<MeasureReportScheduleModel>>
    {
        public bool IsSubmitted { get; set; } = false;
    }

    public class GetMeasureReportSchedulesByIsSubmittedQueryHandler : IRequestHandler<GetMeasureReportSchedulesByIsSubmittedQuery, IEnumerable<MeasureReportScheduleModel>>
    {
        private readonly ILogger<GetMeasureReportSchedulesByIsSubmittedQueryHandler> _logger;
        private readonly MeasureReportScheduleRepository _repository;

        public GetMeasureReportSchedulesByIsSubmittedQueryHandler(ILogger<GetMeasureReportSchedulesByIsSubmittedQueryHandler> logger, MeasureReportScheduleRepository repository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<IEnumerable<MeasureReportScheduleModel>> Handle(GetMeasureReportSchedulesByIsSubmittedQuery request, CancellationToken cancellationToken)
        {
            //Daniel - RC 0.1 Hotfix - LNK-2629
            if (request.IsSubmitted == false)
            {
                return (await _repository.FindAsync(x => x.SubmittedDate == null && x.PatientsToQueryDataRequested != true)).ToEnumerable();
            }

            return (await _repository.FindAsync(x => x.SubmittedDate.HasValue == request.IsSubmitted)).ToEnumerable();
        }
    }
}
