using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Interfaces.Models;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;

public record SftpAcquisitionLogModel(
    Guid? ExternalId, 
    string FacilityId, 
    string FileName, 
    int PatientCount, 
    int EncounterCount, 
    DateTime ProcessDate);

public record PagedSftpAcquisitionLogModel : IPagedModel<SftpAcquisitionLogModel>
{
    public List<SftpAcquisitionLogModel> Records { get; set; }
    public PaginationMetadata Metadata { get; set; }
}

public record CreateSftpLogRequest(string FacilityId, string FileName, DateTime ProcessDate);

public record UpdateSftpLogRequest(Guid ExternalId, string FileName, DateTime ProcessDate);

public static class SftpAcquisitionLogModelExtensions
{

    public static SftpAcquisitionLogModel ToModel(this SftpAcquisitionLog entity) => new(
        entity.ExternalId,
        entity.FacilityId,
        entity.FileName,
        entity.PatientCount,
        entity.EncounterCount,
        entity.ProcessDate
    );
    
    public static SftpAcquisitionLog ToDomain(this SftpAcquisitionLogModel model) => new()
    {
        ExternalId = model.ExternalId ?? Guid.NewGuid(),
        FacilityId = model.FacilityId,
        FileName = model.FileName,
        PatientCount = model.PatientCount,
        EncounterCount = model.EncounterCount,
        ProcessDate = model.ProcessDate
    };
    
    public static SftpAcquisitionLogModel ToModel(this CreateSftpLogRequest req) => new(
        ExternalId: Guid.NewGuid(),
        FacilityId: req.FacilityId,
        FileName: req.FileName,
        PatientCount: 0,
        EncounterCount: 0,
        ProcessDate: req.ProcessDate
    );

    public static SftpAcquisitionLogModel ToModel(this UpdateSftpLogRequest req) => new(
        ExternalId: req.ExternalId,
        FacilityId: string.Empty,
        FileName: req.FileName,
        PatientCount: 0,
        EncounterCount: 0,
        ProcessDate: req.ProcessDate
    );
}