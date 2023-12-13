using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;

namespace LantanaGroup.Link.QueryDispatch.Application.Factory
{
    public class QueryDispatchFactory : IQueryDispatchFactory
    {
        public ScheduledReportEntity CreateScheduledReport(string facilityId, string reportType, DateTime startDate, DateTime endDate, string correlationId)
        {
            return new ScheduledReportEntity()
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = facilityId,
                ReportPeriods = new List<ReportPeriodEntity>()
                {
                    new ReportPeriodEntity()
                    {
                        ReportType = reportType,
                        StartDate = startDate,
                        EndDate = endDate,
                        CreateDate = DateTime.UtcNow,
                        CorrelationId = correlationId
                    }
                },
                CreateDate = DateTime.UtcNow,

            };
        }

        public PatientDispatchEntity CreatePatientDispatch(string facilityId, string patientId, string eventType, string correlationId, ScheduledReportEntity scheduledReportEntity, DispatchSchedule dispatchSchedule)
        {
            DateTime currentDate = DateTime.Now;

            DateTime triggerDate = GetTriggerDate(currentDate, dispatchSchedule);
            List<ReportPeriodEntity> activeReportPeriods = GetActiveReportPeriods(currentDate, scheduledReportEntity.ReportPeriods);

            return new PatientDispatchEntity()
            {
                Id = Guid.NewGuid().ToString(),
                CreateDate = currentDate,
                PatientId = patientId,
                FacilityId = facilityId,
                TriggerDate = triggerDate,
                ScheduledReportPeriods = activeReportPeriods,
                CorrelationId = correlationId
            };
        }

        private DateTime GetTriggerDate(DateTime currentDate, DispatchSchedule dispatchSchedule) 
        {
            switch (dispatchSchedule.DurationType)
            {
                case QueryDispatchConstants.DurationType.Days:
                    return currentDate.AddDays(dispatchSchedule.Duration);
                case QueryDispatchConstants.DurationType.Hours:
                    return currentDate.AddHours(dispatchSchedule.Duration);
                case QueryDispatchConstants.DurationType.Minutes:
                    return currentDate.AddMinutes(dispatchSchedule.Duration);
                case QueryDispatchConstants.DurationType.Seconds:
                    return currentDate.AddSeconds(dispatchSchedule.Duration);
                default:
                    return currentDate;
            }
        }

        private List<ReportPeriodEntity> GetActiveReportPeriods(DateTime currentDate, List<ReportPeriodEntity> reportPeriods)
        {
            return reportPeriods.Where(x => currentDate >= x.StartDate && currentDate <= x.EndDate).ToList();
        }
    }
}
