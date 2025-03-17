using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;

namespace LantanaGroup.Link.QueryDispatch.Application.Factory
{
    public class QueryDispatchFactory : IQueryDispatchFactory
    {
        public ScheduledReportEntity CreateScheduledReport(string facilityId, List<string>reportTypes, Frequency frequency, DateTime startDate, DateTime endDate, string reportTrackingId)
        {
            return new ScheduledReportEntity()
            {
                Id = reportTrackingId,
                FacilityId = facilityId,
                ReportPeriods = new List<ReportPeriodEntity>()
                {
                    new ReportPeriodEntity()
                    {
                        ReportTypes = reportTypes,
                        Frequency = (int)frequency,
                        StartDate = startDate,
                        EndDate = endDate,
                        CreateDate = DateTime.UtcNow,
                        ReportTrackingId = reportTrackingId
                    }
                },
                CreateDate = DateTime.UtcNow,

            };
        }

        public PatientDispatchEntity CreatePatientDispatch(string facilityId, string patientId, string eventType, string correlationId, ScheduledReportEntity scheduledReportEntity, DispatchSchedule dispatchSchedule)
        {
            DateTime currentDate = DateTime.Now;
            var triggerDuration = System.Xml.XmlConvert.ToTimeSpan(dispatchSchedule.Duration);
            var triggerDate = DateTime.Now.Add(triggerDuration);

            return new PatientDispatchEntity()
            {
                Id = Guid.NewGuid().ToString(),
                CreateDate = currentDate,
                PatientId = patientId,
                FacilityId = facilityId,
                TriggerDate = triggerDate,
                ScheduledReportPeriods = scheduledReportEntity.ReportPeriods,
                CorrelationId = correlationId
            };
        }
    }
}