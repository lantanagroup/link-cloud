using LantanaGroup.Link.Census.Application.Models.Messages;

namespace LantanaGroup.Link.Census.Application.Models
{
    public class ConsumePatientIdsAcquiredEventModel
    {
        public string FacilityId { get; set; }
        public PatientIDsAcquired Message { get; set; }
    }
}
