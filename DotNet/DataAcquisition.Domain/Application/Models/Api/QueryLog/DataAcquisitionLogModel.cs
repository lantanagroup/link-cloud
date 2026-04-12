using System.ComponentModel.DataAnnotations;
using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Models;

public class DataAcquisitionLogModel
{
    public long Id { get; init; }
    public string FacilityId { get; set; }
    public bool IsCensus { get; set; }
    public AcquisitionPriority Priority { get; set; }
    public string? PatientId { get; set; }
    public string? ResourceId { get; set; }
    public string? CorrelationId { get; set; }
    public string? ReportTrackingId { get; set; }
    public string? FhirVersion { get; set; }
    public ReportableEvent? ReportableEvent { get; set; }
    public FhirQueryType? QueryType { get; set; }
    public QueryPhase? QueryPhase { get; set; }
    public List<FhirQueryModel> FhirQuery { get; set; } = new List<FhirQueryModel>();
    public RequestStatus? Status { get; set; }
    public DateTime? ExecutionDate { get; set; }
    public DateTime CreateDate { get; set; }
    [MaxLength(64)]
    public string? TraceId { get; set; }
    public int? RetryAttempts { get; set; } = 0;
    public DateTime? CompletionDate { get; set; }
    public long? CompletionTimeMilliseconds { get; set; }
    public List<string>? ResourceAcquiredIds { get; set; } = new List<string>();
    public int ReferenceResourceCount { get; set; }
    public List<string>? Notes { get; set; } = new List<string>();
    public ScheduledReport? ScheduledReport { get; set; }
    public bool IsDeleted { get; set; }

    public static DataAcquisitionLogModel FromDomain(DataAcquisitionLog log)
    {
        if (log == null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        return new DataAcquisitionLogModel
        {
            Id = log.Id,
            Priority = log.Priority,
            FacilityId = log.FacilityId,
            IsCensus = log.IsCensus,
            PatientId = log.PatientId,
            ReportableEvent = log.ReportableEvent,
            ReportTrackingId = log.ReportTrackingId,
            CorrelationId = log.CorrelationId,
            FhirVersion = log.FhirVersion,
            QueryType = log.QueryType,
            QueryPhase = log.QueryPhase,
            FhirQuery = log.FhirQueries != null ? log.FhirQueries.Select(q =>
            new FhirQueryModel
            {
                Id = q.Id,
                FacilityId = q.FacilityId,
                MeasureId = q.MeasureId,
                IdQueryParameterValues = q.IdQueryParameterValues.ToList(),
                IsReference = q.IsReference,
                QueryType = q.QueryType,
                ResourceTypes = q.FhirQueryResourceTypes.Select(s => s.ResourceType).ToList(),
                QueryParameters = q.QueryParameters,
                Paged = q.Paged,
                DataAcquisitionLogId = q.DataAcquisitionLogId,
                ResourceReferenceTypes = q.ResourceReferenceTypes != null ? q.ResourceReferenceTypes.Select(rt => new ResourceReferenceTypeModel
                {
                    Id = rt.Id,
                    FacilityId = rt.FacilityId,
                    QueryPhase = rt.QueryPhase,
                    ResourceType = rt.ResourceType,
                    FhirQueryId = rt.FhirQueryId,
                    CreateDate = rt.CreateDate,
                    ModifyDate = rt.ModifyDate,
                }).ToList() : new()
            }).ToList() : new(),
            Status = log.Status,
            ExecutionDate = log.ExecutionDate,
            CreateDate = log.CreateDate,
            TraceId = log.TraceId,
            RetryAttempts = log.RetryAttempts,
            CompletionDate = log.CompletionDate,
            CompletionTimeMilliseconds = log.CompletionTimeMilliseconds,
            ResourceAcquiredIds = log.ResourceIds?.Select(r => r.ResourceId).ToList() ?? new List<string>(),
            ReferenceResourceCount = log.ReferenceResources?.Count ?? 0,
            Notes = null,
            ScheduledReport = log.ScheduledReportEntity != null
                ? new ScheduledReport
                {
                    ReportTrackingId = log.ScheduledReportEntity.ReportTrackingId,
                    Frequency = log.ScheduledReportEntity.Frequency,
                    StartDate = DateTime.SpecifyKind(log.ScheduledReportEntity.StartDate, DateTimeKind.Utc),
                    EndDate = DateTime.SpecifyKind(log.ScheduledReportEntity.EndDate, DateTimeKind.Utc),
                    ReportTypes = log.ScheduledReportEntity.ReportTypes?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new()
                }
                : null,
            IsDeleted = log.IsDeleted
        };
    }
}
