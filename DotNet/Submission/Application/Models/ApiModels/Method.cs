using LantanaGroup.Link.Submission.Application.Services.Interfaces;
using Microsoft.AspNetCore.Connections.Features;

namespace LantanaGroup.Link.Submission.Application.Models.ApiModels
{
    public class Method
    {
        //public IProtocol Protocol { get; set; }
        //public IAuth Auth { get; set; }
        public DateTime? CreateDate { get; set; }

        public DateTime? ModifyDate { get; set; }
    }
}
