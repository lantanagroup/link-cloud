using LantanaGroup.Link.Census.Models.Messages;

namespace LantanaGroup.Link.Census.Models;

public class PatientEventResponse : BaseResponse
{
    public PatientEvent? PatientEvent { get; set; }
}
