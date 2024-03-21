using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration
{
    public interface ICreateDataAcquisitionRequested
    {
        Task<string> Execute(DataAcquisitionRequested model, string? userId = null);
    }
}
