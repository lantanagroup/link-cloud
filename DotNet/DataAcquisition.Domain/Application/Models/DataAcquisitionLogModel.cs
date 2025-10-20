using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

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
    public string? TimeZone { get; set; }
    [MaxLength(64)]
    public string? TraceId { get; set; }
    public int? RetryAttempts { get; set; } = 0;
    public DateTime? CompletionDate { get; set; }
    public long? CompletionTimeMilliseconds { get; set; }
    public List<string>? ResourceAcquiredIds { get; set; } = new List<string>();
    public List<ReferenceResourceModel> ReferenceResources { get; set; } = new();
    public List<string>? Notes { get; set; } = new List<string>();
    public ScheduledReport? ScheduledReport { get; set; }

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
            PatientId = log?.PatientId,
            ReportableEvent = log.ReportableEvent,
            ReportTrackingId = log?.ReportTrackingId,
            CorrelationId = log?.CorrelationId,
            FhirVersion = log?.FhirVersion,
            QueryType = log.QueryType,
            QueryPhase = log.QueryPhase,
            FhirQuery = log.FhirQueries.Select(FhirQueryModel.FromDomain).ToList(),
            Status = log.Status,
            ExecutionDate = log.ExecutionDate,
            TimeZone = log.TimeZone,
            TraceId = log.TraceId,
            RetryAttempts = log.RetryAttempts,
            CompletionDate = log.CompletionDate,
            CompletionTimeMilliseconds = log.CompletionTimeMilliseconds,
            ResourceAcquiredIds = log.ResourceAcquiredIds,
            ReferenceResources = log.ReferenceResources.Select(ReferenceResourceModel.FromDomain).ToList(),
            Notes = log.Notes,
            ScheduledReport = log.ScheduledReport
        };
    }
}
