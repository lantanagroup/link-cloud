namespace LantanaGroup.Link.DemoApiGateway.Application.models.dataAcquisition
{    public class AuthenticationConfiguration
    {
        public string AuthType { get; set; }
        public string Key { get; set; }
        public string TokenUrl { get; set; }
        public string Audience { get; set; }
        public string ClientId { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }

        public AuthenticationConfiguration() { }
    }
}
