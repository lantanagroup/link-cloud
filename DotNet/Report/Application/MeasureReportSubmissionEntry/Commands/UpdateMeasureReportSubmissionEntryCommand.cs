using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;

namespace LantanaGroup.Link.Report.Application.MeasureReportSubmissionEntry.Commands
{
    public class UpdateMeasureReportSubmissionEntryCommand : IRequest<MeasureReportSubmissionEntryModel>
    {
        public MeasureReportSubmissionEntryModel MeasureReportSubmissionEntry { get; set; } = default!;
    }

    public class UpdateMeasureReportSubmissionEntryCommandHandler : IRequestHandler<UpdateMeasureReportSubmissionEntryCommand, MeasureReportSubmissionEntryModel>
    {
        private readonly ILogger<UpdateMeasureReportSubmissionEntryCommandHandler> _logger;
        private readonly MeasureReportSubmissionEntryRepository _repository;

        public UpdateMeasureReportSubmissionEntryCommandHandler(ILogger<UpdateMeasureReportSubmissionEntryCommandHandler> logger, MeasureReportSubmissionEntryRepository repository)
        {
            _logger = logger;
            _repository = repository;
        }

        public async Task<MeasureReportSubmissionEntryModel> Handle(UpdateMeasureReportSubmissionEntryCommand request, CancellationToken cancellationToken)
        {
            request.MeasureReportSubmissionEntry.ModifyDate = DateTime.UtcNow;
            await _repository.UpdateAsync(request.MeasureReportSubmissionEntry);
            return request.MeasureReportSubmissionEntry;
        }
    }
}
