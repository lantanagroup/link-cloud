using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;

namespace LantanaGroup.Link.Report.Application.MeasureReportSubmissionEntry.Commands
{
    public class CreateMeasureReportSubmissionEntryCommand : IRequest<MeasureReportSubmissionEntryModel>
    {
        public MeasureReportSubmissionEntryModel MeasureReportSubmissionEntry { get; set; } = default!;
    }


    public class CreateMeasureReportSubmissionEntryCommandHandler : IRequestHandler<CreateMeasureReportSubmissionEntryCommand, MeasureReportSubmissionEntryModel>
    {
        private readonly ILogger<CreateMeasureReportSubmissionEntryCommandHandler> _logger;
        private readonly MeasureReportSubmissionEntryRepository _repository;

        public CreateMeasureReportSubmissionEntryCommandHandler(ILogger<CreateMeasureReportSubmissionEntryCommandHandler> logger, MeasureReportSubmissionEntryRepository repository)
        {
            _logger = logger;
            _repository = repository;
        }

        public async Task<MeasureReportSubmissionEntryModel> Handle(CreateMeasureReportSubmissionEntryCommand request, CancellationToken cancellationToken)
        {
            request.MeasureReportSubmissionEntry.Id = string.Empty;
            request.MeasureReportSubmissionEntry.CreateDate = DateTime.UtcNow;

            await _repository.AddAsync(request.MeasureReportSubmissionEntry);

            return request.MeasureReportSubmissionEntry;
        }
    }
}
