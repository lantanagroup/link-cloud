using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using MediatR;

namespace LantanaGroup.Link.Normalization.Application.Commands
{
    public class UnknownOperationCommand : IRequest<OperationCommandResult>
    {
        public Bundle Bundle { get; set; }
        public List<PropertyChangeModel> PropertyChanges { get; set; }
    }

    public class UnknownOperationHandler : IRequestHandler<UnknownOperationCommand, OperationCommandResult>
    {
        public async Task<OperationCommandResult> Handle(UnknownOperationCommand request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
