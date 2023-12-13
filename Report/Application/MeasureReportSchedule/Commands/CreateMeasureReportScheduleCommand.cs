using LantanaGroup.Link.Report.Application.MeasureReportConfig.Queries;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;
using MongoDB.Driver;

namespace LantanaGroup.Link.Report.Application.MeasureReportSchedule.Commands
{
    public class CreateMeasureReportScheduleCommand : IRequest<MeasureReportScheduleModel>
    {
        public MeasureReportScheduleModel ReportSchedule { get; set; } = default!;
    }

    public class CreateMeasureReportScheduleCommandHandler : IRequestHandler<CreateMeasureReportScheduleCommand, MeasureReportScheduleModel>
    {
        private readonly ILogger<CreateMeasureReportScheduleCommandHandler> _logger;
        private readonly MeasureReportScheduleRepository _repository;

        public CreateMeasureReportScheduleCommandHandler(ILogger<CreateMeasureReportScheduleCommandHandler> logger, MeasureReportScheduleRepository repository)
        {
            _logger = logger;
            _repository = repository;
        }

        public async Task<MeasureReportScheduleModel> Handle(CreateMeasureReportScheduleCommand request, CancellationToken cancellationToken)
        {
            request.ReportSchedule.CreateDate = DateTime.UtcNow;
            await _repository.AddAsync(request.ReportSchedule);
            return request.ReportSchedule;
        }
    }
}
