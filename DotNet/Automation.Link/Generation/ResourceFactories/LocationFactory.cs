using Hl7.Fhir.Model;
using static LantanaGroup.Link.Automation.Generation.ResourceFactories.FhirConceptFactory;

namespace LantanaGroup.Link.Automation.Generation.ResourceFactories;

public static class LocationFactory
{
    /// <summary>Generate a Location using well-known type codes (HOSP, ICU, ER, HU).</summary>
    public static Location Generate(string id, string typeCode, string name, string managingOrgId) =>
        Create(id, typeCode, name, managingOrgId);

    /// <summary>Create a Location with caller-supplied values.</summary>
    public static Location Create(string id, string typeCode, string name, string managingOrgId) => new()
    {
        Id = id,
        Status = Location.LocationStatus.Active,
        Name = name,
        Identifier =
        [
            new Identifier { System = "http://example.org/fhir/sid/location", Value = id }
        ],
        Type =
        [
            new CodeableConcept
            {
                Coding = [new Coding("http://terminology.hl7.org/CodeSystem/v3-RoleCode", typeCode, name)]
            }
        ],
        ManagingOrganization = Ref($"Organization/{managingOrgId}"),
        PhysicalType = CC("http://terminology.hl7.org/CodeSystem/location-physical-type", "wa", "Ward")
    };
}
