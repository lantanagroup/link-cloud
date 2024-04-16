using LantanaGroup.Link.Shared.Application.Models;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.PatientResource;

public class TriggerPatientEventsFromBundleCommand : IRequest
{
}

public class TriggerPatientEventsFromBundleCommandHandler : IRequestHandler<TriggerPatientEventsFromBundleCommand>
{
    

    public async Task Handle(TriggerPatientEventsFromBundleCommand request, CancellationToken cancellationToken)
    {
        //KafkaTopic.Res
        throw new NotImplementedException();
    }
}
