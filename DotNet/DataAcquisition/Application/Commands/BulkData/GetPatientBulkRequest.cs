using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.BulkData
{
    public class GetPatientBulkRequest : IRequest<DataAcquiredMessage>
    {
    }
}
