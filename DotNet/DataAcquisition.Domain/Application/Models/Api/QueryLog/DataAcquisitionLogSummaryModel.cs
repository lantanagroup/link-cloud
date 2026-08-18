using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;

/// <summary>
/// Lightweight summary of a DataAcquisitionLog row.
/// Projected directly from the database with no navigation-property hydration
/// (no FhirQuery, ReferenceResources, ResourceReferenceTypes, etc.).
/// Use <see cref="LantanaGroup.Link.DataAcquisition.Domain.Models.DataAcquisitionLogModel"/>
/// or <c>GetWithReferencesAsync</c> when full detail is required.
/// </summary>
public class DataAcquisitionLogSummaryModel
{
    public long Id { get; init; }
    public string FacilityId { get; init; } = null!;
    public AcquisitionPriority Priority { get; init; }
    public string? PatientId { get; init; }
    public string? CorrelationId { get; init; }
    public string? ReportTrackingId { get; init; }
    public string? FhirVersion { get; init; }
    public ReportableEvent? ReportableEvent { get; init; }
    public FhirQueryType? QueryType { get; init; }
    public QueryPhase? QueryPhase { get; init; }
    public RequestStatus? Status { get; init; }
    public DateTime? ExecutionDate { get; init; }
    public DateTime CreateDate { get; init; }
    public string? TraceId { get; init; }
    public int? RetryAttempts { get; init; }
    public DateTime? CompletionDate { get; init; }
    public long? CompletionTimeMilliseconds { get; init; }
    public int ResourceAcquiredCount { get; init; }
    public List<string>? Notes { get; init; }
    public bool IsDeleted { get; init; }
    public bool IsCensus { get; init; }
}
