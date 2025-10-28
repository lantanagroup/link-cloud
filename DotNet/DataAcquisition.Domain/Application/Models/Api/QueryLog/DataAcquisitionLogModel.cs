using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using System.ComponentModel.DataAnnotations;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;

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
            TraceId = log.TraceId,
            RetryAttempts = log.RetryAttempts,
            CompletionDate = log.CompletionDate,
            CompletionTimeMilliseconds = log.CompletionTimeMilliseconds,
            ResourceAcquiredIds = log.ResourceAcquiredIds,
            ReferenceResources = log.ReferenceResources.Select(r => new ReferenceResourceModel
            {
                Id = r.Id,
                FacilityId = r.FacilityId,
                ResourceId = r.ResourceId,
                ResourceType = r.ResourceType,
                ReferenceResource = r.ReferenceResource,
                QueryPhase = r.QueryPhase,
                DataAcquisitionLogId = r.DataAcquisitionLogId
            }).ToList(),
            Notes = log.Notes,
            ScheduledReport = log.ScheduledReport
        };
    }
}
