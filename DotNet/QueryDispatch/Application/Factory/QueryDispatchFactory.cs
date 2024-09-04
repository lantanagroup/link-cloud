﻿using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using QueryDispatch.Application.Settings;

namespace LantanaGroup.Link.QueryDispatch.Application.Factory
{
    public class QueryDispatchFactory : IQueryDispatchFactory
    {
        public ScheduledReportEntity CreateScheduledReport(string facilityId, string reportType, string frequency, DateTime startDate, DateTime endDate, string correlationId)
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
                        Frequency = frequency,
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
