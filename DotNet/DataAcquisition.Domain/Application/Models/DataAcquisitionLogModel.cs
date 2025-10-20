using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using System.Linq;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

public class DataAcquisitionLogModel 
{
    public long? Id { get; init; }
    public string FacilityId { get; set; }
    public AcquisitionPriorityModel Priority { get; set; }
    public string PatientId { get; set; }
    public string? ResourceId { get; set; }
    public string? CorrelationId { get; set; }
    public string? ReportTrackingId { get; set; }
    public string? FhirVersion { get; set; }
    public FhirQueryTypeModel? QueryType { get; set; }
    public QueryPhaseModel? QueryPhase { get; set; }
    public ICollection<FhirQueryModel> FhirQuery { get; set; }
    public RequestStatusModel? Status { get; set; }
    public DateTime? ExecutionDate { get; set; }
    public string? TimeZone { get; set; }
    public int? RetryAttempts { get; set; } = 0;
    public DateTime? CompletionDate { get; set; }
    public long? CompletionTimeMilliseconds { get; set; }
    public List<string>? ResourceAcquiredIds { get; set; } = new List<string>();
    public ICollection<ReferenceResourceModel> ReferenceResources { get; set; }
    public List<string>? Notes { get; set; } = new List<string>();
    public ScheduledReportModel ScheduledReport { get; set; }

    public static DataAcquisitionLogModel FromDomain(DataAcquisitionLog log)
    {
        if (log == null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        return new DataAcquisitionLogModel
        {
            Id = log.Id,
            Priority = AcquisitionPriorityModelUtilities.FromDomain(log.Priority),
            FacilityId = log.FacilityId,
            PatientId = log?.PatientId,
            ReportTrackingId = log?.ReportTrackingId,
            CorrelationId = log?.CorrelationId,
            FhirVersion = log?.FhirVersion,
            QueryType = FhirQueryTypeModelUtilities.FromDomain(log.QueryType.GetValueOrDefault()),
            QueryPhase = QueryPhaseModelUtilities.FromDomain(log.QueryPhase.GetValueOrDefault()),
            FhirQuery = log.FhirQuery?.Select(FhirQueryModel.FromDomain).ToList(),
            Status = RequestStatusModelUtilities.FromDomain(log.Status.GetValueOrDefault()),
            ExecutionDate = log.ExecutionDate,
            TimeZone = log.TimeZone,
            RetryAttempts = log.RetryAttempts,
            CompletionDate = log.CompletionDate,
            CompletionTimeMilliseconds = log.CompletionTimeMilliseconds,
            ResourceAcquiredIds = log.ResourceAcquiredIds,
            ReferenceResources = log.ReferenceResources.Select(ReferenceResourceModel.FromDomain).ToList(),
            Notes = log.Notes,
            ScheduledReport = ScheduledReportModel.FromDomain(log.ScheduledReport)
        };
    }

    public static DataAcquisitionLog ToDomain(DataAcquisitionLogModel model)
    {
        return new DataAcquisitionLog
        {
            Id = model.Id!.Value,
            Priority = AcquisitionPriorityModelUtilities.ToDomain(model.Priority),
            FacilityId = model.FacilityId,
            PatientId = model.PatientId,
            ReportTrackingId = model.ReportTrackingId,
            CorrelationId = model.CorrelationId,
            FhirVersion = model.FhirVersion,
            QueryType = FhirQueryTypeModelUtilities.ToDomain(model.QueryType.Value),
            QueryPhase = QueryPhaseModelUtilities.ToDomain(model.QueryPhase.Value),
            FhirQuery = model.FhirQuery?.Select(FhirQueryModel.ToDomain).ToList(),
            Status = RequestStatusModelUtilities.ToDomain(model.Status.Value),
            ExecutionDate = model.ExecutionDate,
            TimeZone = model.TimeZone,
            RetryAttempts = model.RetryAttempts,
            CompletionDate = model.CompletionDate,
            CompletionTimeMilliseconds = model.CompletionTimeMilliseconds,
            ResourceAcquiredIds = model.ResourceAcquiredIds,
            ReferenceResources = model.ReferenceResources.Select(ReferenceResourceModel.ToDomain).ToList(),
            Notes = model.Notes,
            ScheduledReport = ScheduledReportModel.ToDomain(model.ScheduledReport)
        };
    }
}
