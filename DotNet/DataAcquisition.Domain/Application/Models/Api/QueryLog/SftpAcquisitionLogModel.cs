using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces.Models;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;

public class SftpAcquisitionLogModel
{
    public Guid? ExternalId { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public SftpAcquisitionType AcquisitionType { get; set; }
    public SftpAcquisitionSubType SubType { get; set; } = SftpAcquisitionSubType.None;
    public List<string> FileNames { get; set; } = [];
    public DateTime? ScheduledDate { get; set; }
    public DateTime? ProcessDate { get; set; }
    public int? RetryAttempts { get; set; }
    public RequestStatus Status { get; set; }
    public string? OriginatingTraceId { get; set; }
    public string? OriginatingSpanId { get; set; }
    public List<string> Notes { get; set; } = [];
    public List<SftpAcquisitionBenchmark>? Benchmarks { get; set; }
}

public record PagedSftpAcquisitionLogModel : IPagedModel<SftpAcquisitionLogModel>
{
    public List<SftpAcquisitionLogModel> Records { get; set; } = [];
    public PaginationMetadata Metadata { get; set; } = new();
}

public record CreateSftpLogRequest(
    string FacilityId,
    SftpAcquisitionType AcquisitionType,
    SftpAcquisitionSubType SubType = SftpAcquisitionSubType.None);

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