using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using StackExchange.Redis;

namespace LantanaGroup.Link.QueryDispatch.Application.Interfaces
{
    public interface IQueryDispatchFactory
    {
        ScheduledReportEntity CreateScheduledReport(string facilityId, List<string> reportTypes, string frequency, DateTime startDate, DateTime endDate, string correlationId);
        PatientDispatchEntity CreatePatientDispatch(string facilityId, string patientId, string eventType, string correlationId, ScheduledReportEntity scheduledReportEntity, DispatchSchedule dispatchSchedule);
    }
}