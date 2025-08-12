using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Shared.Application.Utilities
{
    public static class FhirHelperMethods
    {
        public static Organization CreateOrganization(string facilityName, string facilityId, string submittingOrganizationProfile, string organizationTypeSystem, string codeIdSystem, string dataAbsentReasonExtensionUrl, string dataAbsentReasonUnknownCode)
        {
            Organization org = new Organization
            {
                Meta = new Meta
                {
                    Profile = [submittingOrganizationProfile]
                },
                Active = true,
                Id = Guid.NewGuid().ToString() // or National Provider Identifier (NPI) from config?
            };
            CodeableConcept type = new CodeableConcept()
            {
                Coding = [new Coding(organizationTypeSystem, "prov", "Healthcare Provider")]
            };
            org.Type = [type];

            org.Name = facilityName; // should be org name from config?

            org.Identifier.Add(new Identifier
            {
                System = codeIdSystem,
                Value = facilityId // CDC org ID from config
            });

            // TODO: should phone and email be in config?
            // if phone and email not configured add data absent extension
            org.Telecom =
            [
                new ContactPoint
                {
                    Extension = [new Extension(dataAbsentReasonExtensionUrl, new Code(dataAbsentReasonUnknownCode))]
                }
            ];

            // TODO: should be only if address is in config?
            // if no address configured add data absent extension
            org.Address =
            [
                new Address
                {
                    Extension = [new Extension(dataAbsentReasonExtensionUrl, new Code(dataAbsentReasonUnknownCode))]
                }
            ];

            return org;
        }
    }
}
