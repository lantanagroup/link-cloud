using Hl7.Fhir.Model;
using static LantanaGroup.Automation.Generation.ResourceFactories.FhirConceptFactory;

namespace LantanaGroup.Automation.Generation.ResourceFactories;

public static class PatientFactory
{
    /// <summary>Generate a Patient from the archetype pool using a seed index.</summary>
    public static Patient Generate(string id, int seed, string gpPractitionerId)
    {
        var a = FhirGenerationCodes.Patients[seed % FhirGenerationCodes.Patients.Length];
        return Create(id, a.Gender, a.GivenNames, a.FamilyName, a.BirthDate,
                      a.RaceCode, a.RaceDisplay, a.EthCode, a.EthDisplay,
                      a.MaritalCode, a.MaritalDisplay,
                      a.Street, a.City, a.State, a.Zip,
                      a.EmergencyContactName, a.EmergencyContactPhone,
                      gpPractitionerId);
    }

    /// <summary>Create a Patient with fully caller-supplied values.</summary>
    public static Patient Create(
        string id,
        string gender,
        string[] givenNames,
        string familyName,
        string birthDate,
        string raceCode,
        string raceDisplay,
        string ethCode,
        string ethDisplay,
        string maritalCode,
        string maritalDisplay,
        string street,
        string city,
        string state,
        string zip,
        string emergencyContactName,
        string emergencyContactPhone,
        string? gpPractitionerId = null)
    {
        var stableHash = id.GetStableHash32();
        var mrn = $"MRN-{stableHash:D10}";
        var epi = $"E{stableHash & 0xFFFFFF:X6}";
        var areaCode = 500 + (int)(stableHash % 500);
        var lineNum = (int)((stableHash >> 8) % 10000);
        var phone = $"+1 {areaCode:D3}-555-{lineNum:D4}";

        var patient = new Patient
        {
            Id = id,
            Meta = new Meta { Profile = ["http://hl7.org/fhir/us/core/StructureDefinition/us-core-patient"] },
            Active = true,
            Extension =
            [
                new Extension
                {
                    Url       = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-race",
                    Extension =
                    [
                        new Extension("ombCategory", new Coding("urn:oid:2.16.840.1.113883.6.238", raceCode, raceDisplay)),
                        new Extension("text",        new FhirString(raceDisplay))
                    ]
                },
                new Extension
                {
                    Url       = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-ethnicity",
                    Extension =
                    [
                        new Extension("ombCategory", new Coding("urn:oid:2.16.840.1.113883.6.238", ethCode, ethDisplay)),
                        new Extension("text",        new FhirString(ethDisplay))
                    ]
                }
            ],
            Identifier =
            [
                new Identifier
                {
                    Use    = Identifier.IdentifierUse.Usual,
                    System = "urn:oid:1.2.840.114350.1.13.93.3.7.3.688884.100",
                    Type   = new CodeableConcept { Coding = [new Coding("http://terminology.hl7.org/CodeSystem/v2-0203", "AN", "Account number")], Text = "CEID" },
                    Value  = id
                },
                new Identifier
                {
                    Use    = Identifier.IdentifierUse.Official,
                    System = "urn:oid:2.16.840.1.113883.3.16.100.1",
                    Type   = new CodeableConcept { Coding = [new Coding("http://terminology.hl7.org/CodeSystem/v2-0203", "MR", "Medical record number")], Text = "MRN" },
                    Value  = mrn
                },
                new Identifier
                {
                    Use    = Identifier.IdentifierUse.Usual,
                    System = "urn:oid:1.2.840.114350.1.13.93.3.7.5.737384.0",
                    Type   = new CodeableConcept { Coding = [new Coding("http://terminology.hl7.org/CodeSystem/v2-0203", "PI", "Patient internal identifier")], Text = "EPI" },
                    Value  = epi
                },
                new Identifier
                {
                    Use    = Identifier.IdentifierUse.Secondary,
                    System = "http://open.epic.com/FHIR/StructureDefinition/patient-fhir-id",
                    Type   = new CodeableConcept { Text = "FHIR ID" },
                    Value  = id
                },
            ],
            Name =
            [
                new HumanName
                {
                    Use    = HumanName.NameUse.Official,
                    Family = familyName,
                    Given  = givenNames,
                    Text   = $"{string.Join(" ", givenNames)} {familyName}"
                }
            ],
            Telecom =
            [
                new ContactPoint { System = ContactPoint.ContactPointSystem.Phone, Value = phone,                    Use = ContactPoint.ContactPointUse.Home },
                new ContactPoint { System = ContactPoint.ContactPointSystem.Email, Value = $"{id}@patient.example.com", Use = ContactPoint.ContactPointUse.Home }
            ],
            Gender = gender switch { "male" => AdministrativeGender.Male, "female" => AdministrativeGender.Female, _ => AdministrativeGender.Other },
            BirthDate = birthDate,
            Deceased = new FhirBoolean(false),
            Address =
            [
                new Address
                {
                    Use        = Address.AddressUse.Home,
                    Line       = [street],
                    City       = city,
                    State      = state,
                    PostalCode = zip,
                    Country    = "US"
                }
            ],
            MaritalStatus = CC("http://terminology.hl7.org/CodeSystem/v3-MaritalStatus", maritalCode, maritalDisplay),
            Contact =
            [
                new Patient.ContactComponent
                {
                    Relationship = [CC("http://terminology.hl7.org/CodeSystem/v2-0131", "C", "Emergency Contact")],
                    Name         = new HumanName { Use = HumanName.NameUse.Official, Text = emergencyContactName },
                    Telecom      = [new ContactPoint { System = ContactPoint.ContactPointSystem.Phone, Value = emergencyContactPhone, Use = ContactPoint.ContactPointUse.Mobile }]
                }
            ],
            Communication =
            [
                new Patient.CommunicationComponent
                {
                    Language  = CC("urn:ietf:bcp:47", "en", "English"),
                    Preferred = true
                }
            ]
        };

        if (gpPractitionerId != null)
            patient.GeneralPractitioner = [Ref($"Practitioner/{gpPractitionerId}")];

        return patient;
    }
}
