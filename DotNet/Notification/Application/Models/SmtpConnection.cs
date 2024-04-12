namespace LantanaGroup.Link.Notification.Application.Models
{
    public class SmtpConnection
    {       
        //TODO: Look into moving username/password into vault service when available

        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string EmailFrom { get; set; } = string.Empty;
        public bool UseBasicAuth { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool UseOAuth2 { get; set; }
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;

    }
}
