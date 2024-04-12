using LantanaGroup.Link.Report.Application.MeasureReportConfig.Queries;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;

namespace LantanaGroup.Link.Report.Application.MeasureReportConfig.Commands
{
    public class UpdateMeasureReportSubmissionCommand : IRequest<MeasureReportSubmissionModel>
    {
        public MeasureReportSubmissionModel MeasureReportSubmission { get; set; } = default!;
    }

    public class UpdateMeasureReportSubmissionCommandHandler : IRequestHandler<UpdateMeasureReportSubmissionCommand, MeasureReportSubmissionModel>
    {
        private readonly ILogger<UpdateMeasureReportSubmissionCommandHandler> _logger;
        private readonly MeasureReportSubmissionRepository _repository;

        public UpdateMeasureReportSubmissionCommandHandler(ILogger<UpdateMeasureReportSubmissionCommandHandler> logger, MeasureReportSubmissionRepository repository)
        {
            _logger = logger;
            _repository = repository;
        }

        public async Task<MeasureReportSubmissionModel> Handle(UpdateMeasureReportSubmissionCommand request, CancellationToken cancellationToken)
        {
            var submission = await _repository.GetAsync(request.MeasureReportSubmission.Id);
            if (submission == null)
            {
                throw new Exception($"No report submission found for {request.MeasureReportSubmission.Id}");
            }

            submission.MeasureReportScheduleId = request.MeasureReportSubmission.MeasureReportScheduleId;
            submission.SubmissionBundle = request.MeasureReportSubmission.SubmissionBundle;
            submission.ModifyDate = DateTime.UtcNow;
            await _repository.UpdateAsync(submission);
            return submission;
        }
    }
}
