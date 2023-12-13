using LantanaGroup.Link.Report.Application.MeasureReportConfig.Queries;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;

namespace LantanaGroup.Link.Report.Application.MeasureReportConfig.Commands
{
    public class UpdateMeasureReportConfigCommand : IRequest<MeasureReportConfigModel>
    {
        public MeasureReportConfigModel MeasureReportConfig { get; set; } = default!;
    }

    public class UpdateMeasureReportConfigCommandHandler : IRequestHandler<UpdateMeasureReportConfigCommand, MeasureReportConfigModel>
    {
        private readonly ILogger<UpdateMeasureReportConfigCommandHandler> _logger;
        private readonly MeasureReportConfigRepository _repository;

        public UpdateMeasureReportConfigCommandHandler(ILogger<UpdateMeasureReportConfigCommandHandler> logger, MeasureReportConfigRepository repository)
        {
            _logger = logger;
            _repository = repository;
        }

        public async Task<MeasureReportConfigModel> Handle(UpdateMeasureReportConfigCommand request, CancellationToken cancellationToken)
        {
            var config = await _repository.GetAsync(request.MeasureReportConfig.Id, cancellationToken);
            if (config== null)
            {
                throw new Exception($"No config found for {request.MeasureReportConfig.Id}");
            }

            config.FacilityId = request.MeasureReportConfig.FacilityId;
            config.ReportType = request.MeasureReportConfig.ReportType;
            config.BundlingType = request.MeasureReportConfig.BundlingType;
            config.ModifyDate = DateTime.UtcNow;
            await _repository.UpdateAsync(config);
            return config;
        }
    }
}
