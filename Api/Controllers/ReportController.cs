using LantanaGroup.Link.Tenant;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc;
using LantanaGroup.Link.Tenant.Client;
using System.Net;

namespace LantanaGroup.Link.Api.Controllers
{
    [ApiController]
    [Route("api/api-report")]
    public class ReportController : ControllerBase
    {
        private readonly ILogger<ReportController> _logger;
        private readonly IConfiguration _configuration;

        public ReportController(ILogger<ReportController> logger, IConfiguration config)
        {
            _logger = logger;
            _configuration = config;
        }

        [HttpPost]
        public void CreateRequest()
        {
            HttpClient client = new HttpClient();
            TenantClient tenantClient = new TenantClient("http://localhost:7332", client);

            Console.WriteLine("Test");
        }

        private void test()
        {
            var channel = GrpcChannel.ForAddress(this._configuration.GetValue<string>("TenantService:Address"), new GrpcChannelOptions()
            {
                Credentials = this._configuration.GetValue<bool>("TenantService:Secure") ? Grpc.Core.ChannelCredentials.SecureSsl : Grpc.Core.ChannelCredentials.Insecure
            });
            TenantSvc.TenantSvcClient client = new TenantSvc.TenantSvcClient(channel);

            HelloRequest req = new HelloRequest();
            req.Name = "Test From Api";
            client.SayHello(req);
        }
    }
}