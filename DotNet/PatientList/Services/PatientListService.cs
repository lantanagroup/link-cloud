using Grpc.Core;
using LantanaGroup.Link.PatientList;

namespace LantanaGroup.Link.PatientList.Services
{
    public class PatientListService : PatientListSvc.PatientListSvcBase
    {
        private readonly ILogger<PatientListService> _logger;
        public PatientListService(ILogger<PatientListService> logger)
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