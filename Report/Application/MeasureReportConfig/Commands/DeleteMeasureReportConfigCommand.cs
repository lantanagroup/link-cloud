using LantanaGroup.Link.Report.Repositories;
using MediatR;

namespace LantanaGroup.Link.Report.Application.MeasureReportConfig.Commands
{
    public class DeleteMeasureReportConfigCommand : IRequest
    {
        public string Id { get; set; } = string.Empty;
    }

    public class DeleteMeasureReportConfigCommandHandler : IRequestHandler<DeleteMeasureReportConfigCommand>
    {
        private readonly ILogger<DeleteMeasureReportConfigCommandHandler> _logger;
        private readonly MeasureReportConfigRepository _repository;

        public DeleteMeasureReportConfigCommandHandler(ILogger<DeleteMeasureReportConfigCommandHandler> logger, MeasureReportConfigRepository repository)
        {
            _logger = logger;
            _repository = repository;
        }

        public async Task Handle(DeleteMeasureReportConfigCommand request, CancellationToken cancellationToken)
        {
            await _repository.DeleteAsync(request.Id, cancellationToken);
        }
    }
}
