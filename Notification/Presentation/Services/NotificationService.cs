using Grpc.Core;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries;
using LantanaGroup.Link.Notification.Protos;
using System.Diagnostics;
using static LantanaGroup.Link.Notification.Settings.NotificationConstants;

namespace LantanaGroup.Link.Notification.Presentation.Services
{
    public class NotificationService : Protos.NotificationService.NotificationServiceBase
    {
        private readonly ILogger<NotificationService> _logger;
        private readonly IGetFacilityConfigurationQuery _getFacilityConfigurationQuery;

        public NotificationService(ILogger<NotificationService> logger, IGetFacilityConfigurationQuery getFacilityConfigurationQuery)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger)); 
            _getFacilityConfigurationQuery = getFacilityConfigurationQuery ?? throw new ArgumentNullException(nameof(getFacilityConfigurationQuery));
        }

        public override async Task<FacilityConfigurationReply> GetFacilityConfiguration(FacilityConfigurationMessage model, ServerCallContext context)
        {
            //TODO check for authorization      

            try
            {
                if (string.IsNullOrEmpty(model.FacilityId)) 
                {
                    var serviceEx = new ArgumentNullException("Failed to execute the request to get a facility configuration, no facility id was provided.");        
                    throw serviceEx;
                }

                //add id to current activity
                var activity = Activity.Current;
                activity?.AddTag("facility.id", model.FacilityId);

                NotificationConfigurationModel config = await _getFacilityConfigurationQuery.Execute(model.FacilityId);

                FacilityConfigurationReply reply = new FacilityConfigurationReply
                {
                    Id = config.Id.ToString(),
                    FacilityId = model.FacilityId                    
                };

                if(config.EmailAddresses is not null)
                {
                    reply.EmailAddresses.AddRange(config.EmailAddresses);
                }                
                
                if(config.EnabledNotifications is not null)
                {                    
                    foreach (var e in config.EnabledNotifications)
                    {
                        EnabledNotificationMessage enabled = new EnabledNotificationMessage();
                        enabled.NotificationType = e.NotificationType;
                        enabled.Recipients.AddRange(e.Recipients);
                        reply.EnabledNotifications.Add(enabled);
                    }
                }       
                
                if(config.Channels is not null)
                {
                    foreach (var c in config.Channels)
                    {
                        FacilityChannelMessage channel = new FacilityChannelMessage();
                        channel.Name = c.Name;
                        channel.Enabled = c.Enabled;
                        reply.Channels.Add(channel);
                    }
                }

                return reply;
            }
            catch (Exception ex)
            {
                ex.Data.Add("message", model);
                _logger.LogError(new EventId(NotificationLoggingIds.GetItem, "Notification Service - Get notifications by facility id"), ex, "An exception occurred in rpc request while attempting to retrieve the nofitication configuration of a facility with an id of '{facilityId}'", model.FacilityId);
                throw;
            }

        }

    }
}
