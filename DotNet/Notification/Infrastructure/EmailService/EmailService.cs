using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using MimeKit;
using MimeKit.Text;
using System.Diagnostics;
using static LantanaGroup.Link.Notification.Settings.NotificationConstants;

namespace LantanaGroup.Link.Notification.Infrastructure.EmailService
{
    public class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;
        private readonly IOptions<SmtpConnection> _smtpConfig;

        public EmailService(ILogger<EmailService> logger, IOptions<SmtpConnection> smtpConfig)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _smtpConfig = smtpConfig ?? throw new ArgumentNullException(nameof(smtpConfig));
        }

        public async Task<bool> Send(string id, List<string> to, List<string>? bcc, string subject, string message, string? from = null)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Email Channel Service - Send");

            // create message
            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(_smtpConfig.Value.EmailFrom));
            foreach (string recipient in to)
            {
                email.To.Add(MailboxAddress.Parse(recipient));
            }
            email.Subject = subject;
            email.Body = new TextPart(TextFormat.Plain) { Text = message };
            if (bcc is not null)
            {
                foreach (string recipient in bcc)
                {
                    email.Bcc.Add(MailboxAddress.Parse(recipient));
                }
            }

            if (_smtpConfig.Value.UseOAuth2)
            {
                return await SendWithOauthAsync(email, id);
            }
            else
            {
                return SendBySMTP(email, id);
            }
        }

        private bool SendBySMTP(MimeMessage email, string id)
        {
            using var smtp = new SmtpClient();
            try
            {
                // send email
                using (ServiceActivitySource.Instance.StartActivity("Connecting to SMTP server and sending email"))
                {
                    smtp.Connect(_smtpConfig.Value.Host, _smtpConfig.Value.Port, SecureSocketOptions.StartTls);

                    //check if using basic authentication
                    if (_smtpConfig.Value.UseBasicAuth)
                    {
                        if (!string.IsNullOrEmpty(_smtpConfig.Value.Username) && !string.IsNullOrEmpty(_smtpConfig.Value.Password))
                        {
                            smtp.Authenticate(_smtpConfig.Value.Username, _smtpConfig.Value.Password);
                        }
                        else
                        {
                            _logger.LogInformation(new EventId(NotificationLoggingIds.EmailChannel, "Notification Service - Send Email"), "Email configuration issue using Basic Authentication: No username and/or password provided.");
                            throw new ArgumentNullException("Email Service: Error using Basic Authentication: No username and/or password provided.");
                        }
                    }

                    smtp.Send(email);
                    smtp.Disconnect(true);
                    _logger.LogInformation(new EventId(NotificationLoggingIds.EmailChannel, "Notification Service - Send Email"), "Successfully sent email notification '{id}'", id);
                    return true;
                }

            }
            catch (Exception ex)
            {
                smtp.Disconnect(true);
                _logger.LogError(new EventId(NotificationLoggingIds.EmailChannel, "Notification Service - Send Email"), ex, "Failed to send email notification '{id}'.", id);
                var currentActivity = Activity.Current;
                currentActivity?.SetStatus(ActivityStatusCode.Error, "Failed to send email through SMTP server.");
                throw;
            }
        }

        private async Task<bool> SendWithOauthAsync(MimeMessage email, string id)
        {
            if (!string.IsNullOrEmpty(_smtpConfig.Value.ClientId) && !string.IsNullOrEmpty(_smtpConfig.Value.TenantId))
            {
                using var smtp = new SmtpClient();
                try
                {
                    var options = new ConfidentialClientApplicationOptions
                    {
                        ClientId = _smtpConfig.Value.ClientId,
                        ClientSecret = _smtpConfig.Value.ClientSecret,
                        TenantId = _smtpConfig.Value.TenantId
                    };

                    var confidentialClientApplication = ConfidentialClientApplicationBuilder
                        .CreateWithApplicationOptions(options)
                        .Build();

                    var scopes = new string[] {
                        "https://outlook.office365.com/.default"
                    };

                    var authToken = await confidentialClientApplication.AcquireTokenForClient(scopes).ExecuteAsync();
                    var oauth2 = new SaslMechanismOAuth2(_smtpConfig.Value.Username, authToken.AccessToken); //"link -support@nhsnlink.org" 

                    await smtp.ConnectAsync(_smtpConfig.Value.Host, _smtpConfig.Value.Port, SecureSocketOptions.StartTls);
                    await smtp.AuthenticateAsync(oauth2);
                    smtp.Send(email);
                    smtp.Disconnect(true);
                    _logger.LogInformation(new EventId(NotificationLoggingIds.EmailChannel, "Notification Service - Send Email"), "Successfully sent email notification '{id}'", id);
                    return true;
                }
                catch (Exception ex)
                {
                    smtp.Disconnect(true);
                    _logger.LogError(new EventId(NotificationLoggingIds.EmailChannel, "Notification Service - Send Email"), ex, "Failed to send email notification '{id}'.", id);
                    var currentActivity = Activity.Current;
                    currentActivity?.SetStatus(ActivityStatusCode.Error, "Failed to send email through SMTP server.");
                    throw;
                }
            }
            else
            {
                _logger.LogInformation(new EventId(NotificationLoggingIds.EmailChannel, "Notification Service - Send Email"), "Email configuration issue using Basic Authentication: No username and/or password provided.");
                throw new ArgumentNullException("Email Service: Error using Basic Authentication: No username and/or password provided.");
            }


        }
    }
}