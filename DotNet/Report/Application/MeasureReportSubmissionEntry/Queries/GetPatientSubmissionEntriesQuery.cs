using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;
using MongoDB.Driver;

namespace LantanaGroup.Link.Report.Application.MeasureReportSubmissionEntry.Queries
{
    public class GetPatientSubmissionEntriesQuery : IRequest<List<MeasureReportSubmissionEntryModel>>
    {
        public string FacilityId { get; set; }
        public string PatientId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class GetPatientSubmissionEntriesQueryHandler : IRequestHandler<GetPatientSubmissionEntriesQuery, List<MeasureReportSubmissionEntryModel>>
    {
        private readonly ILogger<GetPatientSubmissionEntriesQueryHandler> _logger;
        private readonly MeasureReportSubmissionEntryRepository _entriesRepository;
        private readonly MeasureReportScheduleRepository _scheduleRepository;

        public GetPatientSubmissionEntriesQueryHandler(ILogger<GetPatientSubmissionEntriesQueryHandler> logger, MeasureReportSubmissionEntryRepository entriesRepository, MeasureReportScheduleRepository scheduleRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _entriesRepository = entriesRepository ?? throw new ArgumentNullException(nameof(entriesRepository));
            _scheduleRepository = scheduleRepository ?? throw new ArgumentNullException(nameof(scheduleRepository));
        }

        public async Task<List<MeasureReportSubmissionEntryModel>> Handle(GetPatientSubmissionEntriesQuery request, CancellationToken cancellationToken)
        {
            var reportSchedules = (await _scheduleRepository.FindAsync(r =>
                r.ReportStartDate == request.StartDate
                && r.ReportEndDate == request.EndDate
                && r.FacilityId == request.FacilityId, cancellationToken)).ToList().Select(x => x.Id);

            return await (await _entriesRepository.FindAsync(x => x.PatientId == request.PatientId && reportSchedules.Contains(x.MeasureReportScheduleId), cancellationToken)).ToListAsync(cancellationToken);
        }
    }
}
