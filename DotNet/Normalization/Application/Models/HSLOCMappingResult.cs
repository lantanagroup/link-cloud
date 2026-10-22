using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Models.Mapping;

namespace LantanaGroup.Link.Normalization.Application.Models;

public class HSLOCMappingResult
{
    public string FacilityId { get; set; }
    public Location Location {get;set;}
    public List<HSLOCMappingResultCode> LocationTypeCodes { get; set; } = new();

    public HSLOCMappingResult(string facilityId, Location location)
    {
        FacilityId = facilityId;
        Location = location;
    }
}

public class HSLOCMappingResultCode
{
    public string? SourceSystem {get;set;}
    public string? SourceCode {get;set;}
    public string? TargetCode {get;set;}
}