using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Interfaces.Models;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;

public record SftpAcquisitionLogModel(
    long Id, 
    Guid? ExternalId, 
    string FacilityId, 
    string FacilityName, 
    int PatientCount, 
    int EncounterCount, 
    DateTime ProcessDate);

public record PagedSftpAcquisitionLogModel : IPagedModel<SftpAcquisitionLogModel>
{
    public List<SftpAcquisitionLogModel> Records { get; set; }
    public PaginationMetadata Metadata { get; set; }
}

public static class SftpAcquisitionLogModelExtensions
{

    public static SftpAcquisitionLogModel ToModel(this SftpAcquisitionLog entity) => new(
        entity.Id,
        entity.ExternalId,
        entity.FacilityId,
        entity.FacilityName,
        entity.PatientCount,
        entity.EncounterCount,
        entity.ProcessDate
    );
    
    public static SftpAcquisitionLog ToDomain(this SftpAcquisitionLogModel model) => new()
    {
        ExternalId = model.ExternalId ?? Guid.NewGuid(),
        FacilityId = model.FacilityId,
        FacilityName = model.FacilityName,
        PatientCount = model.PatientCount,
        EncounterCount = model.EncounterCount,
        ProcessDate = model.ProcessDate
    };

}