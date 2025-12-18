using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;

public record SftpAcquisitionLogModel(
    long Id, 
    Guid? ExternalId, 
    string FacilityId, 
    string FacilityName, 
    int PatientCount, 
    int EncounterCount, 
    DateTime ProcessDate);


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