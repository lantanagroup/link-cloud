using Grpc.Core;
using LantanaGroup.Link.Validation;

namespace Validation.Services
{
    public class ValidationService : ValidationSvc.ValidationSvcBase
    {
        private readonly ILogger<ValidationService> _logger;
        public ValidationService(ILogger<ValidationService> logger)
        {
            _logger = logger;
        }

        public override async Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {
            //return await Task.FromResult(new HelloReply
            //{
            //    Message = "Hello " + request.Name
            //});
            return new HelloReply
            {
                Message = "Hello " + request.Name
            };
        }
    }
}