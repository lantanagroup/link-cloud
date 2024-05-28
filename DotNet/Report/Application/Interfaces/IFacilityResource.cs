using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Report.Application.Interfaces
{
    public interface IFacilityResource
    {
        public string GetId();
        public bool IsPatientResource();
        public Resource Resource();
    }
}
