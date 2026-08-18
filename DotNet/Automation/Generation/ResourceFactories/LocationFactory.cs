using Hl7.Fhir.Model;
using static LantanaGroup.Automation.Generation.ResourceFactories.FhirConceptFactory;

namespace LantanaGroup.Automation.Generation.ResourceFactories;

public static class LocationFactory
{
    /// <summary>Generate a Location using well-known type codes (HOSP, ICU, ER, HU).</summary>
    public static Location Generate(string id, string typeCode, string name, string managingOrgId, string? partOfId = null) =>
        Create(id, typeCode, name, managingOrgId, partOfId);

    /// <summary>Create a Location with caller-supplied values.</summary>
    public static Location Create(string id, string typeCode, string name, string managingOrgId, string? partOfId = null)
    {
        // Map v3-RoleCode to CDC HSLOC codes used by NHSN "Inpatient, Emergency, and Observation Locations" value set
        var (hslocCode, hslocDisplay) = typeCode switch
        {
            "ICU" => ("1025-6", "Trauma Critical Care"),
            "ER"  => ("1108-0", "Emergency Department"),
            "HU"  => ("1093-4", "Step Down Unit"),
            "HOSP"=> ("1060-3", "Medical Ward"),
            "OF"  => ("1160-1", "Outpatient Clinic"),
            _     => ("1060-3", "Medical Ward"),
        };

        return new Location
        {
            Id = id,
            Status = Location.LocationStatus.Active,
            Name = name,
            Alias = new List<string> { "Alias, " + name },
            Identifier =
            [
                new Identifier { System = "http://example.org/fhir/sid/location", Value = id }
            ],
            Type =
            [
                new CodeableConcept
                {
                    Coding =
                    [
                        new Coding("http://terminology.hl7.org/CodeSystem/v3-RoleCode", typeCode, name),
                        new Coding("https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html", hslocCode, hslocDisplay)
                    ]
                }
            ],
            ManagingOrganization = Ref($"Organization/{managingOrgId}"),
            PhysicalType = CC("http://terminology.hl7.org/CodeSystem/location-physical-type", "wa", "Ward"),
            PartOf = partOfId is not null ? Ref($"Location/{partOfId}") : null
        };
    }
}
