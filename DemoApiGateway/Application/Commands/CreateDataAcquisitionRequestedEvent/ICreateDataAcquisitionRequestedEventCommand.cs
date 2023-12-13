using LantanaGroup.Link.DemoApiGateway.Application.models.testing;

namespace LantanaGroup.Link.DemoApiGateway.Application.Commands.CreateDataAcquisitionRequestedEvent
{
    public interface ICreateDataAcquisitionRequestedEventCommand
    {
        Task<string> Execute(DataAcquisitionRequested model);
    }
}
