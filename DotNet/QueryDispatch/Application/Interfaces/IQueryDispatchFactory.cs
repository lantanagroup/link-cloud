
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.QueryDispatch.Application.Interfaces
{
    public interface IQueryDispatchFactory
    {
        ScheduledReportEntity CreateScheduledReport(string facilityId, List<string> reportTypes, Frequency frequency, DateTime startDate, DateTime endDate, string reportTrackingId);
        PatientDispatchEntity CreatePatientDispatch(string facilityId, string patientId, string eventType, string correlationId, ScheduledReportEntity scheduledReportEntity, DispatchSchedule dispatchSchedule);
    }
}