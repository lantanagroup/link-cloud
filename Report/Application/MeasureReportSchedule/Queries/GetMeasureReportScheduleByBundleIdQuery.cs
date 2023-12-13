using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;
using MongoDB.Driver;

namespace LantanaGroup.Link.Report.Application.MeasureReportSchedule.Queries
{
    public class GetMeasureReportScheduleByBundleIdQuery : IRequest<MeasureReportScheduleModel>
    {
        public string ReportBundleId { get; set; } = string.Empty;
    }

    public class GetMeasureReportSchedulesByIsSubmittedHandler : IRequestHandler<GetMeasureReportScheduleByBundleIdQuery, MeasureReportScheduleModel>
    {
        private readonly ILogger<GetMeasureReportSchedulesByIsSubmittedHandler> _logger;
        private readonly MeasureReportScheduleRepository _scheduleRepository;
        private readonly MeasureReportSubmissionRepository _submissionRepository;
        
        public GetMeasureReportSchedulesByIsSubmittedHandler(ILogger<GetMeasureReportSchedulesByIsSubmittedHandler> logger, MeasureReportScheduleRepository scheduleRepository, MeasureReportSubmissionRepository submissionRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _scheduleRepository = scheduleRepository ?? throw new ArgumentNullException(nameof(scheduleRepository));
            _submissionRepository = submissionRepository ?? throw new ArgumentNullException(nameof(submissionRepository));
        }

        public async Task<MeasureReportScheduleModel> Handle(GetMeasureReportScheduleByBundleIdQuery request, CancellationToken cancellationToken)
        {
            var bundle = (await _submissionRepository.FindAsync(x => x.Id == request.ReportBundleId)).FirstOrDefault();
            if (bundle is null)
                return null;

            return (await _scheduleRepository.FindAsync(x => x.Id == bundle.MeasureReportScheduleId)).FirstOrDefault();
        }
    }
}
