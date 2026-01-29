using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces.Models;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;

public class SftpAcquisitionLogModel
{
    public Guid? ExternalId { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public SftpAcquisitionType AcquisitionType { get; set; }
    public List<string> FileNames { get; set; } = [];
    public DateTime? ProcessDate { get; set; }
    public int? RetryAttempts { get; set; }
    public string? OriginatingTraceId { get; set; }
    public string? OriginatingSpanId { get; set; }
    public List<string> Notes { get; set; } = [];
}

public record PagedSftpAcquisitionLogModel : IPagedModel<SftpAcquisitionLogModel>
{
    public List<SftpAcquisitionLogModel> Records { get; set; } = [];
    public PaginationMetadata Metadata { get; set; } = new();
}

public record CreateSftpLogRequest(
    string FacilityId,
    SftpAcquisitionType AcquisitionType);

public static class SftpAcquisitionLogModelExtensions
{
    public static SftpAcquisitionLogModel ToModel(this SftpAcquisitionLog entity) => new()
    {
        ExternalId = entity.ExternalId,
        FacilityId = entity.FacilityId,
        AcquisitionType = entity.AcquisitionType,
        FileNames = entity.FileNames,
        ProcessDate = entity.ProcessDate.HasValue
            ? DateTime.SpecifyKind(entity.ProcessDate.Value, DateTimeKind.Utc)
            : null,
        RetryAttempts = entity.RetryAttempts,
        OriginatingTraceId = entity.OriginatingTraceId,
        OriginatingSpanId = entity.OriginatingSpanId,
        Notes = entity.Notes
    };

    public static SftpAcquisitionLog ToDomain(this SftpAcquisitionLogModel model) => new()
    {
        ExternalId = model.ExternalId ?? Guid.NewGuid(),
        FacilityId = model.FacilityId,
        AcquisitionType = model.AcquisitionType,
        FileNames = model.FileNames,
        ProcessDate = model.ProcessDate,
        RetryAttempts = model.RetryAttempts,
        OriginatingTraceId = model.OriginatingTraceId,
        OriginatingSpanId = model.OriginatingSpanId,
        Notes = model.Notes
    };

    public static SftpAcquisitionLogModel ToModel(this CreateSftpLogRequest req) => new()
    {
        ExternalId = Guid.NewGuid(),
        FacilityId = req.FacilityId,
        AcquisitionType = req.AcquisitionType
    };
}