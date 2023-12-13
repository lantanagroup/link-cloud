using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.BulkData;

public class GetBulkDataRequest : IRequest<DataAcquiredMessage>
{
}

public class GetBulkDataHandler : IRequestHandler<GetBulkDataRequest, DataAcquiredMessage>
{
    public async Task<DataAcquiredMessage> Handle(GetBulkDataRequest request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
