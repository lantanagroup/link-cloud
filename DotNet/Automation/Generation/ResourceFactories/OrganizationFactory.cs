using Hl7.Fhir.Model;
using static LantanaGroup.Automation.Generation.ResourceFactories.FhirConceptFactory;

namespace LantanaGroup.Automation.Generation.ResourceFactories;

public static class OrganizationFactory
{
    /// <summary>Generate a hospital Organization from the pool using a seed index.</summary>
    public static Organization Generate(string id, string name = "General Test Hospital", string alias = "GTH") =>
        Create(id, name, alias);

    /// <summary>Create an Organization with caller-supplied values.</summary>
    public static Organization Create(string id, string name, string alias) => new()
    {
        Id = id,
        Meta = new Meta { Profile = ["http://hl7.org/fhir/us/core/StructureDefinition/us-core-organization"] },
        Active = true,
        Identifier =
        [
            new Identifier { System = "https://github.com/synthetichealth/synthea", Value = id }
        ],
        Type = [CC("http://terminology.hl7.org/CodeSystem/organization-type", "prov", "Healthcare Provider")],
        Name = name,
        Alias = [alias],
        Telecom = [new ContactPoint { System = ContactPoint.ContactPointSystem.Phone, Value = "555-867-5309" }],
        Address =
        [
            new Address { Line = ["100 Hospital Drive"], City = "TestCity", State = "TX", PostalCode = "75001", Country = "US" }
        ]
    };
}
