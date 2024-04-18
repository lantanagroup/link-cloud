using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;
using MongoDB.Driver;
using System.Xaml.Permissions;

namespace LantanaGroup.Link.Report.Application.MeasureReportSubmissionEntry.Commands
{
    public class GetMeasureReportSubmissionEntryCommand : IRequest<MeasureReportSubmissionEntryModel>
    {
        public string MeasureReportScheduleId { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
    }

    public class GetMeasureReportSubmissionEntryCommandHandler : IRequestHandler<GetMeasureReportSubmissionEntryCommand, MeasureReportSubmissionEntryModel>
    {
        private readonly MeasureReportSubmissionEntryRepository _repository;

        public GetMeasureReportSubmissionEntryCommandHandler(ILogger<GetMeasureReportSubmissionEntryCommand> logger, MeasureReportSubmissionEntryRepository repository)
        {
            _repository = repository;
        }

        public async Task<MeasureReportSubmissionEntryModel> Handle(GetMeasureReportSubmissionEntryCommand request, CancellationToken cancellationToken)
        {
            var measureReportSubmissionEntry = await _repository.FindAsync(x => x.MeasureReportScheduleId == request.MeasureReportScheduleId && x.PatientId == request.PatientId);

            return measureReportSubmissionEntry.FirstOrDefault();
        }
    }
}
