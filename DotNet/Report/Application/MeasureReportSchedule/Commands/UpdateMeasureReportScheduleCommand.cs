using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;

namespace LantanaGroup.Link.Report.Application.MeasureReportSchedule.Commands
{
    public class UpdateMeasureReportScheduleCommand : IRequest<MeasureReportScheduleModel>
    {
        public MeasureReportScheduleModel ReportSchedule { get; set; } = default!;
    }

    public class UpdateReportScheduleCommandHandler : IRequestHandler<UpdateMeasureReportScheduleCommand, MeasureReportScheduleModel>
    {
        private readonly ILogger<UpdateReportScheduleCommandHandler> _logger;
        private readonly MeasureReportScheduleRepository _repository;

        public UpdateReportScheduleCommandHandler(ILogger<UpdateReportScheduleCommandHandler> logger, MeasureReportScheduleRepository repository)
        {
            _logger = logger;
            _repository = repository;
        }

        public async Task<MeasureReportScheduleModel> Handle(UpdateMeasureReportScheduleCommand request, CancellationToken cancellationToken)
        {
            var schedule = await _repository.GetAsync(request.ReportSchedule.Id);
            if (schedule == null)
            {
                throw new Exception($"No report schedule found for {request.ReportSchedule.Id}");
            }

            schedule.FacilityId = request.ReportSchedule.FacilityId;
            schedule.ReportStartDate = request.ReportSchedule.ReportStartDate;
            schedule.ReportEndDate = request.ReportSchedule.ReportEndDate;
            schedule.PatientsToQuery = request.ReportSchedule.PatientsToQuery;
            schedule.SubmittedDate = request.ReportSchedule.SubmittedDate;
            schedule.PatientsToQueryDataRequested = request.ReportSchedule.PatientsToQueryDataRequested;
            schedule.ModifyDate = DateTime.UtcNow;
            await _repository.UpdateAsync(schedule, cancellationToken);
            return schedule;
        }
    }
}
