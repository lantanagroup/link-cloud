using LantanaGroup.Link.Report.Application.MeasureReportConfig.Queries;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;

namespace LantanaGroup.Link.Report.Application.MeasureReportConfig.Commands
{
    public class CreateMeasureReportConfigCommand : IRequest<MeasureReportConfigModel>
    {
        public MeasureReportConfigModel MeasureReportConfig { get; set; } = default!;
    }

    public class CreateMeasureReportConfigCommandHandler : IRequestHandler<CreateMeasureReportConfigCommand, MeasureReportConfigModel>
    {
        private readonly ILogger<CreateMeasureReportConfigCommandHandler> _logger;
        private readonly MeasureReportConfigRepository _repository;

        public CreateMeasureReportConfigCommandHandler(ILogger<CreateMeasureReportConfigCommandHandler> logger, MeasureReportConfigRepository repository)
        {
            _logger = logger;
            _repository = repository;
        }

        public async Task<MeasureReportConfigModel> Handle(CreateMeasureReportConfigCommand request, CancellationToken cancellationToken)
        {
            //TODO: Rather than checking if Id exists, I think we need to check if facility/reporttype for the config exists
            if (!string.IsNullOrWhiteSpace(request.MeasureReportConfig.Id)
                || (await _repository.GetAsync(request.MeasureReportConfig.Id)) != null)
            {
                request.MeasureReportConfig.CreateDate = DateTime.UtcNow;
                if (await _repository.AddAsync(request.MeasureReportConfig))
                {
                    return request.MeasureReportConfig;
                }
                else
                {
                    return new MeasureReportConfigModel();
                }
            }
            else
            {
                return null;
            }
        }
    }
}
