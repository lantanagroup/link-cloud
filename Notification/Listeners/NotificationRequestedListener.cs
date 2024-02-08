using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.Notification.Commands;
using LantanaGroup.Link.Notification.Application.Notification.Queries;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries;
using LantanaGroup.Link.Notification.Infrastructure;
using LantanaGroup.Link.Notification.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models;
using System.Diagnostics;

namespace LantanaGroup.Link.Notification.Listeners
{
    public class NotificationRequestedListener : BackgroundService
    {
        private readonly ILogger<NotificationRequestedListener> _logger;
        private readonly ICreateNotificationCommand _createNotificationCommand;
        private readonly IValidateEmailAddressCommand _validateEmailAddressCommand;
        private readonly IGetNotificationQuery _getNotificationQuery;
        private readonly IGetFacilityConfigurationQuery _getFacilityConfigurationQuery;
        private readonly ISendNotificationCommand _sendNotificationCommand;
        private readonly INotificationFactory _notificationFactory;
        private readonly IKafkaConsumerFactory _kafkaConsumerFactory;

        public NotificationRequestedListener(ILogger<NotificationRequestedListener> logger, ICreateNotificationCommand createNotificationCommand, IValidateEmailAddressCommand validateEmailAddressCommand, 
            IGetFacilityConfigurationQuery getFacilityConfigurationQuery, IGetNotificationQuery getNotificationQuery, ISendNotificationCommand sendNotificationCommand, 
            INotificationFactory notificationFactory, IKafkaConsumerFactory kafkaConsumerFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _createNotificationCommand = createNotificationCommand ?? throw new ArgumentNullException(nameof(createNotificationCommand));
            _validateEmailAddressCommand = validateEmailAddressCommand ?? throw new ArgumentNullException(nameof(validateEmailAddressCommand));
            _getNotificationQuery = getNotificationQuery ?? throw new ArgumentNullException(nameof(getNotificationQuery));
            _getFacilityConfigurationQuery = getFacilityConfigurationQuery ?? throw new ArgumentNullException(nameof(getFacilityConfigurationQuery));
            _sendNotificationCommand = sendNotificationCommand ?? throw new ArgumentNullException(nameof(sendNotificationCommand));
            _notificationFactory = notificationFactory ?? throw new ArgumentNullException(nameof(notificationFactory));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentNullException(nameof(kafkaConsumerFactory));
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async void StartConsumerLoop(CancellationToken cancellationToken)
        {
            using (var _consumer = _kafkaConsumerFactory.CreateNotificationRequestedConsumer(enableAutoCommit: false))
            {
                try
                {
                    _consumer.Subscribe(nameof(KafkaTopic.NotificationRequested));
                    _logger.LogConsumerStarted(nameof(KafkaTopic.NotificationRequested), DateTime.UtcNow);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {                            
                            await _consumer.ConsumeWithInstrumentation(async (result, cancellationToken) =>
                            {
                                if (result != null && result.Message.Value != null)
                                {                                    
                                    var currentActivity = Activity.Current;
                                    if (currentActivity != null)
                                    {
                                        currentActivity.AddTag("link.service", ServiceActivitySource.Instance.Name);
                                        currentActivity.AddTag("link.service.version", ServiceActivitySource.Instance.Version);
                                    }

                                    NotificationMessage messageValue = result.Message.Value;

                                    if (result.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
                                    {
                                        messageValue.CorrelationId = System.Text.Encoding.UTF8.GetString(headerValue);
                                    }                                   

                                    _logger.LogNotificationRequestedConsumption(messageValue.NotificationType);

                                    //remove any email addresses that are not valid
                                    List<string> recipients = new List<string>();
                                    List<string> bccs = new List<string>();
                                    using (ServiceActivitySource.Instance.StartActivity("Remove invalid email addresses from consumed message"))
                                    {
                                        if (messageValue.Recipients is not null)
                                        {
                                            foreach (var recipient in messageValue.Recipients)
                                            {
                                                bool isValid = await _validateEmailAddressCommand.Execute(recipient);
                                                if (!isValid)
                                                {
                                                    _logger.LogNotificationRequestedInvalidEmailAddress(recipient);
                                                }
                                                else
                                                {
                                                    recipients.Add(recipient);
                                                }
                                            }
                                        }                                        

                                        if (messageValue.Bcc is not null)
                                        {
                                            foreach (var recipient in messageValue.Bcc)
                                            {
                                                bool isValid = await _validateEmailAddressCommand.Execute(recipient);
                                                if (!isValid)
                                                {
                                                    _logger.LogNotificationRequestedInvalidEmailAddress(recipient);
                                                }
                                                else 
                                                { 
                                                    bccs.Add(recipient); 
                                                }
                                            }
                                        }
                                    }

                                    //create notification
                                    CreateNotificationModel notificationModel = _notificationFactory.CreateNotificationModelCreate(messageValue.NotificationType, result.Message.Key, messageValue.CorrelationId, messageValue.Subject, messageValue.Body, recipients, bccs);                                               

                                    string notificationId = await _createNotificationCommand.Execute(notificationModel);
                                    _logger.LogNotificationCreation(notificationId, notificationModel);

                                    //send notification
                                    NotificationModel notification = await _getNotificationQuery.Execute(notificationId);
                                    SendNotificationModel sendModel = _notificationFactory.CreateSendNotificationModel(notification.Id, notification.Recipients, notification.Bcc, notification.Subject, notification.Body);

                                    //if a facility based notification, get their configuration and add it to the send model
                                    if (!string.IsNullOrEmpty(result.Message.Key))
                                    {
                                        NotificationConfigurationModel config = await _getFacilityConfigurationQuery.Execute(result.Message.Key);
                                        sendModel.FacilityConfig = config;
                                    }

                                    //asynchrounously send the email
                                    _ = Task.Run(() => _sendNotificationCommand.Execute(sendModel));

                                    //consume the result and offset
                                    _consumer.Commit(result);
                                }

                            }, cancellationToken);

                        }
                        catch (ConsumeException ex)
                        {
                            Activity.Current?.SetStatus(ActivityStatusCode.Error);
                            _logger.LogConsumerException(nameof(KafkaTopic.NotificationRequested), ex.Message);
                            if (ex.Error.IsFatal)
                            {
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Activity.Current?.SetStatus(ActivityStatusCode.Error);
                            _logger.LogConsumerException(nameof(KafkaTopic.NotificationRequested), ex.Message);
                            break;
                        }
                    }

                    _consumer.Close();
                    _consumer.Dispose();

                }
                catch (OperationCanceledException oce)
                {
                    Activity.Current?.SetStatus(ActivityStatusCode.Error);
                    _logger.LogOperationCanceledException(nameof(KafkaTopic.AuditableEventOccurred), oce.Message);
                    _consumer.Close();
                    _consumer.Dispose();
                }
            }
        }

    }
}
