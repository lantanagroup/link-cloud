using Grpc.Core;

namespace LantanaGroup.Link.DataAcquisition.Application.Services
{
    public class DataAcquisitionService : DataAcquisitionSvc.DataAcquisitionSvcBase
    {
        private readonly ILogger<DataAcquisitionService> _logger;

        public DataAcquisitionService(ILogger<DataAcquisitionService> logger)
        {
            _logger = logger;
        }

        public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {
            return Task.FromResult(new HelloReply
            {
                Message = "Hello " + request.Name
            });
        }
    }
}