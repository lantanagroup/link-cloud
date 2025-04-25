using DataAcquisition.Domain.Models.Enums;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Application.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResourceTypeModel
{
    PatientList,
    Encounter,
    Condition,
    MedicationRequest,
    Observation,
    Procedure,
    ServiceRequest,
    Coverage,
    MedicationAdminisitration
}

public static class ResourceTypeModelUtilities
{
    public static ResourceTypeModel FromDomain(ResourceType resourceType)
    {
        return resourceType switch
        {
            ResourceType.PatientList => ResourceTypeModel.PatientList,
            ResourceType.Encounter => ResourceTypeModel.Encounter,
            ResourceType.Condition => ResourceTypeModel.Condition,
            ResourceType.MedicationRequest => ResourceTypeModel.MedicationRequest,
            ResourceType.Observation => ResourceTypeModel.Observation,
            ResourceType.Procedure => ResourceTypeModel.Procedure,
            ResourceType.ServiceRequest => ResourceTypeModel.ServiceRequest,
            ResourceType.Coverage => ResourceTypeModel.Coverage,
            ResourceType.MedicationAdminisitration => ResourceTypeModel.MedicationAdminisitration,
            _ => throw new Exception($"Unknown resource type: {resourceType}"),
        };
    }

    public static ResourceType ToDomain(ResourceTypeModel resourceType)
    {
        return resourceType switch
        {
            ResourceTypeModel.PatientList => ResourceType.PatientList,
            ResourceTypeModel.Encounter => ResourceType.Encounter,
            ResourceTypeModel.Condition => ResourceType.Condition,
            ResourceTypeModel.MedicationRequest => ResourceType.MedicationRequest,
            ResourceTypeModel.Observation => ResourceType.Observation,
            ResourceTypeModel.Procedure => ResourceType.Procedure,
            ResourceTypeModel.ServiceRequest => ResourceType.ServiceRequest,
            ResourceTypeModel.Coverage => ResourceType.Coverage,
            ResourceTypeModel.MedicationAdminisitration => ResourceType.MedicationAdminisitration,
            _ => throw new Exception($"Unknown resource type model: {resourceType}"),
        };
    }
}
