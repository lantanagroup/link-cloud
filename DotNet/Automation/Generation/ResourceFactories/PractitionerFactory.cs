using Hl7.Fhir.Model;
using static LantanaGroup.Automation.Generation.ResourceFactories.FhirConceptFactory;

namespace LantanaGroup.Automation.Generation.ResourceFactories;

public static class PractitionerFactory
{
    /// <summary>Generate a Practitioner from the pool using a seed index.</summary>
    public static Practitioner Generate(string id, int seed)
    {
        var p = FhirGenerationCodes.Practitioners[seed % FhirGenerationCodes.Practitioners.Length];
        return Create(id, p.Family, p.Given, p.Gender, p.Npi, p.Email, p.Specialty);
    }

    /// <summary>Create a Practitioner with caller-supplied values.</summary>
    public static Practitioner Create(
        string id, string family, string given, string gender,
        string npi, string email, string specialty) => new()
        {
            Id = id,
            Meta = new Meta { Profile = ["http://hl7.org/fhir/us/core/StructureDefinition/us-core-practitioner"] },
            Active = true,
            Identifier =
        [
            new Identifier
            {
                System = "http://hl7.org/fhir/sid/us-npi",
                Value  = npi,
                Type   = new CodeableConcept { Coding = [new Coding("http://terminology.hl7.org/CodeSystem/v2-0203", "NPI", "National provider identifier")] }
            }
        ],
            Name =
        [
            new HumanName
            {
                Use    = HumanName.NameUse.Official,
                Family = family,
                Given  = [given],
                Prefix = ["Dr."],
                Text   = $"Dr. {given} {family}"
            }
        ],
            Telecom =
        [
            new ContactPoint
            {
                System    = ContactPoint.ContactPointSystem.Email,
                Value     = email,
                Use       = ContactPoint.ContactPointUse.Work,
                Extension =
                [
                    new Extension("http://hl7.org/fhir/us/core/StructureDefinition/us-core-direct", new FhirBoolean(true))
                ]
            }
        ],
            Address =
        [
            new Address
            {
                Use        = Address.AddressUse.Work,
                Line       = ["100 Hospital Drive"],
                City       = "Dallas",
                State      = "TX",
                PostalCode = "75201",
                Country    = "US"
            }
        ],
            Gender = gender switch { "male" => AdministrativeGender.Male, "female" => AdministrativeGender.Female, _ => AdministrativeGender.Other },
            Qualification =
        [
            new Practitioner.QualificationComponent
            {
                Code       = CC("http://terminology.hl7.org/CodeSystem/v2-0360", "MD", "Doctor of Medicine"),
                Identifier = [new Identifier { System = "http://hl7.org/fhir/sid/us-npi", Value = npi }]
            }
        ]
        };
}
