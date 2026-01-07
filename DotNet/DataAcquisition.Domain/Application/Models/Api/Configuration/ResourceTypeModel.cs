using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;

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
    MedicationAdminisitration,
    Location,
    DiagnosticReport, 
    Device, 
    Medication, 
}

public static class ResourceTypeModelUtilities
{
    public static ResourceTypeModel FromDomain(Hl7.Fhir.Model.ResourceType resourceType)
    {
        return resourceType switch
        {
            Hl7.Fhir.Model.ResourceType.List => ResourceTypeModel.PatientList,
            Hl7.Fhir.Model.ResourceType.Encounter => ResourceTypeModel.Encounter,
            Hl7.Fhir.Model.ResourceType.Condition => ResourceTypeModel.Condition,
            Hl7.Fhir.Model.ResourceType.MedicationRequest => ResourceTypeModel.MedicationRequest,
            Hl7.Fhir.Model.ResourceType.Observation => ResourceTypeModel.Observation,
            Hl7.Fhir.Model.ResourceType.Procedure => ResourceTypeModel.Procedure,
            Hl7.Fhir.Model.ResourceType.ServiceRequest => ResourceTypeModel.ServiceRequest,
            Hl7.Fhir.Model.ResourceType.Coverage => ResourceTypeModel.Coverage,
            Hl7.Fhir.Model.ResourceType.MedicationAdministration => ResourceTypeModel.MedicationAdminisitration,
            Hl7.Fhir.Model.ResourceType.Location => ResourceTypeModel.Location,
            _ => throw new Exception($"Unknown resource type: {resourceType}"),
        };
    }

    public static Hl7.Fhir.Model.ResourceType ToDomain(ResourceTypeModel resourceType)
    {
        return resourceType switch
        {
            ResourceTypeModel.PatientList => Hl7.Fhir.Model.ResourceType.List,
            ResourceTypeModel.Encounter => Hl7.Fhir.Model.ResourceType.Encounter,
            ResourceTypeModel.Condition => Hl7.Fhir.Model.ResourceType.Condition,
            ResourceTypeModel.MedicationRequest => Hl7.Fhir.Model.ResourceType.MedicationRequest,
            ResourceTypeModel.Observation => Hl7.Fhir.Model.ResourceType.Observation,
            ResourceTypeModel.Procedure => Hl7.Fhir.Model.ResourceType.Procedure,
            ResourceTypeModel.ServiceRequest => Hl7.Fhir.Model.ResourceType.ServiceRequest,
            ResourceTypeModel.Coverage => Hl7.Fhir.Model.ResourceType.Coverage,
            ResourceTypeModel.MedicationAdminisitration => Hl7.Fhir.Model.ResourceType.MedicationAdministration,
            ResourceTypeModel.Location => Hl7.Fhir.Model.ResourceType.Location,
            _ => throw new Exception($"Unknown resource type model: {resourceType}"),
        };
    }

    public static Hl7.Fhir.Model.ResourceType ToDomain(string resourceType)
    {
        return resourceType switch
        {
            "PatientList" => Hl7.Fhir.Model.ResourceType.List,
            "Encounter" => Hl7.Fhir.Model.ResourceType.Encounter,
            "Condition" => Hl7.Fhir.Model.ResourceType.Condition,
            "MedicationRequest" => Hl7.Fhir.Model.ResourceType.MedicationRequest,
            "Observation" => Hl7.Fhir.Model.ResourceType.Observation,
            "Procedure" => Hl7.Fhir.Model.ResourceType.Procedure,
            "ServiceRequest" => Hl7.Fhir.Model.ResourceType.ServiceRequest,
            "Coverage" => Hl7.Fhir.Model.ResourceType.Coverage,
            "MedicationAdminisitration" => Hl7.Fhir.Model.ResourceType.MedicationAdministration,
            "Location" => Hl7.Fhir.Model.ResourceType.Location,
            _ => throw new Exception($"Unknown resource type model: {resourceType}"),
        };
    }
}
