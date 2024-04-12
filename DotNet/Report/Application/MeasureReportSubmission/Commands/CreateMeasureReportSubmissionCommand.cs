using LantanaGroup.Link.Report.Application.MeasureReportConfig.Queries;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;

namespace LantanaGroup.Link.Report.Application.MeasureReportConfig.Commands
{
    public class CreateMeasureReportSubmissionCommand : IRequest<MeasureReportSubmissionModel>
    {
        public MeasureReportSubmissionModel MeasureReportSubmission { get; set; } = default!;
    }

    public class CreateMeasureReportSubmissionCommandHandler : IRequestHandler<CreateMeasureReportSubmissionCommand, MeasureReportSubmissionModel>
    {
        private readonly ILogger<CreateMeasureReportSubmissionCommandHandler> _logger;
        private readonly MeasureReportSubmissionRepository _repository;

        public CreateMeasureReportSubmissionCommandHandler(ILogger<CreateMeasureReportSubmissionCommandHandler> logger, MeasureReportSubmissionRepository repository)
        {
            _logger = logger;
            _repository = repository;
        }

        public async Task<MeasureReportSubmissionModel> Handle(CreateMeasureReportSubmissionCommand request, CancellationToken cancellationToken)
        {
            request.MeasureReportSubmission.CreateDate = DateTime.UtcNow;
            await _repository.AddAsync(request.MeasureReportSubmission);
            return request.MeasureReportSubmission;
        }
    }
}
