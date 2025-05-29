using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory;

namespace DataAcquisitionTests.Models.Factory
{
    public class ReferenceResourceBundleExtractorTest
    {
        [Fact]
        public void Extract_ElementNameMatchesResourceType_IsFound()
        {
            Encounter encounter = new();
            ResourceReference reference = new("EpisodeOfCare/the-episode-of-care");
            encounter.EpisodeOfCare.Add(reference);
            List<ResourceReference> references = ReferenceResourceBundleExtractor.Extract(encounter, ["EpisodeOfCare"]);
            Assert.Equal([reference], references);
        }

        [Fact]
        public void Extract_ElementNameDoesNotMatchResourceType_IsFound()
        {
            Encounter encounter = new();
            ResourceReference reference = new("ServiceRequest/the-service-request");
            encounter.BasedOn.Add(reference);
            List<ResourceReference> references = ReferenceResourceBundleExtractor.Extract(encounter, ["ServiceRequest"]);
            Assert.Equal([reference], references);
        }
    }
}
