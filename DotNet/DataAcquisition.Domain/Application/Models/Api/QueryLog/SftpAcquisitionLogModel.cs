using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;

[DataContract]
public class SftpAcquisitionLogModel
{
    [DataMember]
    public Guid? ExternalId { get; set; }

    [DataMember]
    public string FacilityId { get; set; } = string.Empty;

    [DataMember]
    public SftpAcquisitionType AcquisitionType { get; set; }

    [DataMember]
    public SftpAcquisitionSubType SubType { get; set; } = SftpAcquisitionSubType.None;

    [DataMember]
    public List<string> FileNames { get; set; } = [];

    [DataMember]
    public DateTime? ScheduledDate { get; set; }

    [DataMember]
    public DateTime? ProcessDate { get; set; }

    [DataMember]
    public int? RetryAttempts { get; set; }

    [DataMember]
    public RequestStatus Status { get; set; }

    [DataMember]
    public string? OriginatingTraceId { get; set; }

    [DataMember]
    public string? OriginatingSpanId { get; set; }

    [DataMember]
    public List<string> Notes { get; set; } = [];

    [DataMember]
    public List<SftpAcquisitionBenchmark>? Benchmarks { get; set; }
}

public record PagedSftpAcquisitionLogModel : IPagedModel<SftpAcquisitionLogModel>
{
    public List<SftpAcquisitionLogModel> Records { get; set; } = [];
    public PaginationMetadata Metadata { get; set; } = new();
}

[DataContract]
public record CreateSftpLogRequest(
    [property: DataMember] string FacilityId,
    [property: DataMember] SftpAcquisitionType AcquisitionType,
    [property: DataMember] SftpAcquisitionSubType SubType = SftpAcquisitionSubType.None);

public static class SftpAcquisitionLogModelExtensions
{
    public static SftpAcquisitionLogModel ToModel(this SftpAcquisitionLog entity) => new()
    {
        ExternalId = entity.ExternalId,
        FacilityId = entity.FacilityId,
        AcquisitionType = entity.AcquisitionType,
        SubType = entity.SubType,
        FileNames = entity.FileNames,
        ScheduledDate = entity.ScheduledDate.HasValue
            ? DateTime.SpecifyKind(entity.ScheduledDate.Value, DateTimeKind.Utc)
            : null,
        ProcessDate = entity.ProcessDate.HasValue
            ? DateTime.SpecifyKind(entity.ProcessDate.Value, DateTimeKind.Utc)
            : null,
        RetryAttempts = entity.RetryAttempts,
        Status = entity.Status,
        OriginatingTraceId = entity.OriginatingTraceId,
        OriginatingSpanId = entity.OriginatingSpanId,
        Notes = entity.Notes,
        Benchmarks = entity.Benchmarks
    };

    public static SftpAcquisitionLog ToDomain(this SftpAcquisitionLogModel model) => new()
    {
        ExternalId = model.ExternalId ?? Guid.NewGuid(),
        FacilityId = model.FacilityId,
        AcquisitionType = model.AcquisitionType,
        SubType = model.SubType,
        FileNames = model.FileNames,
        ScheduledDate = model.ScheduledDate,
        ProcessDate = model.ProcessDate,
        RetryAttempts = model.RetryAttempts,
        Status = model.Status,
        OriginatingTraceId = model.OriginatingTraceId,
        OriginatingSpanId = model.OriginatingSpanId,
        Notes = model.Notes,
        Benchmarks = model.Benchmarks
    };

    public static SftpAcquisitionLogModel ToModel(this CreateSftpLogRequest req) => new()
    {
        ExternalId = Guid.NewGuid(),
        FacilityId = req.FacilityId,
        AcquisitionType = req.AcquisitionType,
        SubType = req.SubType,
        Status = RequestStatus.Pending
    };
}