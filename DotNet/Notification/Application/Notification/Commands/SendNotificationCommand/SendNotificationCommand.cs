using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Domain.Entities;
using LantanaGroup.Link.Notification.Infrastructure;
using LantanaGroup.Link.Notification.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace LantanaGroup.Link.Notification.Application.Notification.Commands
{
    public class SendNotificationCommand : ISendNotificationCommand
    {
        private readonly ILogger<SendNotificationCommand> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptions<Channels> _channels;
        private readonly IEmailService _emailService;
        private readonly INotificationServiceMetrics _metrics;

        public SendNotificationCommand(ILogger<SendNotificationCommand> logger, IOptions<Channels> channels, IEmailService emailService, INotificationServiceMetrics metrics, IServiceScopeFactory scopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _channels = channels ?? throw new ArgumentNullException(nameof(channels));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));            
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));            
        }

        public async Task<bool> Execute(SendNotificationModel model)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Send Notification Command");

            if (string.IsNullOrEmpty(model.Id)) { throw new ArgumentNullException(nameof(model.Id)); }

            if (string.IsNullOrEmpty(model.Subject)) { throw new ArgumentNullException(nameof(model.Subject)); }

            if (string.IsNullOrEmpty(model.Message)) { throw new ArgumentNullException(nameof(model.Message)); }

            //check if test indicators are enabled
            if (_channels.Value.IncludeTestMessage)
            {
                model.Subject = $"{_channels.Value.SubjectTestMessage} - {model.Subject}";
                model.Message = $"{_channels.Value.TestMessage}{Environment.NewLine}{Environment.NewLine}{model.Message}";
            }

            try 
            {
                //check that email notifications have been enabled
                if (_channels.Value.Email)                
                {
                    if (model.FacilityConfig is null || (model.FacilityConfig.Channels is not null && model.FacilityConfig.Channels.Any(x => x.Name.Equals(ChannelType.Email.ToString()) && x.Enabled)))
                    {
                        var currentActivity = Activity.Current;
                        var sentDate = DateTimeOffset.UtcNow;
                        currentActivity?.AddEvent(new("Sending request to email channel service.", sentDate));
                        await _emailService.Send(model.Id, model.Recipients, model.Bcc, model.Subject, model.Message, null);
                        _logger.LogNotificationSent(nameof(ChannelType.Email), sentDate.DateTime);
                            
                        using (ServiceActivitySource.Instance.StartActivity("Set notification sent on date"))
                        {
                            //create scope to get repository independant of current scope
                            //this allows this command to be run asynchonously regardless of the scope that called it
                            using (var scope = _scopeFactory.CreateScope())
                            {
                                var _datastore = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
                                await _datastore.SetNotificationSentOnAsync(NotificationId.FromString(model.Id));
                            }                                

                            //add id to current activity                            
                            currentActivity?.AddTag(DiagnosticNames.NotificationId, model.Id);
                            currentActivity?.AddTag(DiagnosticNames.FacilityId, model.FacilityConfig?.FacilityId);

                            //update notification creation metric counter                            
                            _metrics.IncrementNotificationSentCounter([
                                new KeyValuePair<string, object?>(DiagnosticNames.NotificationId, model.Id),
                                new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, model.FacilityConfig?.FacilityId),
                                new KeyValuePair<string, object?>(DiagnosticNames.RecipientCount, model.Recipients.Count),
                                new KeyValuePair<string, object?>(DiagnosticNames.NotificationChannel, nameof(ChannelType.Email))
                            ]);
                        }
                        
                    }
                    else
                    {
                        var message = $"Facility {model.FacilityConfig.FacilityId} does not have channels configured for email notifications. Did not send notification {model.Id}.";
                        _logger.LogNotificationSentWarning("Email", message);
                        Activity.Current?.SetStatus(ActivityStatusCode.Ok, "Facility does not have channels configured for email notifications. Did not send notification.");
                    }
                }
                
                return true;
            }
            catch(Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogNotificationSentException("Email", ex.Message);
                throw;
            }

        }
    }
}
