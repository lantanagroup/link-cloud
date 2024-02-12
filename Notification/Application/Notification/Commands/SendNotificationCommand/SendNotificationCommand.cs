﻿using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Infrastructure;
using LantanaGroup.Link.Notification.Infrastructure.Logging;
using LantanaGroup.Link.Notification.Infrastructure.Telemetry;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace LantanaGroup.Link.Notification.Application.Notification.Commands
{
    public class SendNotificationCommand : ISendNotificationCommand
    {
        private readonly ILogger<SendNotificationCommand> _logger;
        private readonly IOptions<Channels> _channels;
        private readonly IEmailService _emailService;
        private readonly INotificationRepository _datastore;
        private readonly NotificationServiceMetrics _metrics;

        public SendNotificationCommand(ILogger<SendNotificationCommand> logger, IOptions<Channels> channels, IEmailService emailService, INotificationRepository datastore, NotificationServiceMetrics metrics)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _channels = channels ?? throw new ArgumentNullException(nameof(channels));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));
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
                        _logger.LogNotificationSent("Email", sentDate.DateTime);
                            
                        using (ServiceActivitySource.Instance.StartActivity("Set notification sent on date"))
                        {
                            await _datastore.SetNotificationSentOn(model.Id);

                            //add id to current activity                            
                            currentActivity?.AddTag("notification id", model.Id);
                            currentActivity?.AddTag("facility id", model.FacilityConfig?.FacilityId);

                            //update notification creation metric counter                            
                            _metrics.NotificationSentCounter.Add(1, 
                                new KeyValuePair<string, object?>("facility", model.FacilityConfig?.FacilityId),
                                new KeyValuePair<string, object?>("channel", "Email"));
                        }
                        
                    }
                    else
                    {
                        var message = $"Facility {model.FacilityConfig.FacilityId} does not have channels configured for email notifications. Did not send notification {model.Id}.";
                        _logger.LogNotificationSentWarning("Email", message);                        
                        var currentActivity = Activity.Current;
                        currentActivity?.SetStatus(ActivityStatusCode.Ok, "Facility does not have channels configured for email notifications. Did not send notification.");
                    }
                }
                
                return true;
            }
            catch(Exception ex)
            {                
                var currentActivity = Activity.Current;
                currentActivity?.SetStatus(ActivityStatusCode.Error);
                _logger.LogNotificationSentException("Email", ex.Message);
                throw;
            }

        }
    }
}
