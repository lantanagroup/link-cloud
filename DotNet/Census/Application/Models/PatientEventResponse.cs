using LantanaGroup.Link.Census.Application.Models.Messages;

namespace LantanaGroup.Link.Census.Application.Models;

public class PatientEventResponse : BaseResponse
{
    public PatientEvent? PatientEvent { get; set; }
}
