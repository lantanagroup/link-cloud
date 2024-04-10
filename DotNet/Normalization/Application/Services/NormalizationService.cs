
using Grpc.Core;

namespace LantanaGroup.Link.Normalization.Application.Services
{
    public class NormalizationService : NormalizationSvc.NormalizationSvcBase
    {
        private readonly ILogger<NormalizationService> _logger;
        public NormalizationService(ILogger<NormalizationService> logger)
        {
            _logger = logger;
        }

        public override async Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {
            return new HelloReply
            {
                Message = "Hello " + request.Name
            };
        }
    }
}