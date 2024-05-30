using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Domain.Enums;
using MongoDB.Driver;

namespace LantanaGroup.Link.Report.Application.Interfaces
{
    public interface IFacilityResource
    {
        public string GetId();
        public Resource GetResource();
    }
}
