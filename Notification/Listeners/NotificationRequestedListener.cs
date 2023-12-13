using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.Notification.Commands;
using LantanaGroup.Link.Notification.Application.Notification.Queries;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries;
using LantanaGroup.Link.Notification.Infrastructure;
using LantanaGroup.Link.Shared.Application.Models;
using System.Diagnostics;
using System.Text.RegularExpressions;
using static LantanaGroup.Link.Notification.Settings.NotificationConstants;

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
            using (var _notificationRequestedConsumer = _kafkaConsumerFactory.CreateNotificationRequestedConsumer())
            {
                try
                {
                    _notificationRequestedConsumer.Subscribe(nameof(KafkaTopic.NotificationRequested));
                    _logger.LogInformation(new EventId(NotificationLoggingIds.KafkaConsumer, "Notification Service - Notification requested consumer"), "Started consumer for topic '{notificationRequested}' at {date}.", nameof(KafkaTopic.NotificationRequested), DateTime.UtcNow);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            //var consumeResult = _notificationRequestedConsumer.Consume(cancellationToken);
                            await _notificationRequestedConsumer.ConsumeWithInstrumentation(async (result, cancellationToken) =>
                            {
                                if (result != null && result.Message.Value != null)
                                {
                                    //using Activity? activity = ServiceActivitySource.Instance.StartActivity("Kafka - Consume Notification Requested Message");
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
                                        _logger.LogInformation(new EventId(NotificationLoggingIds.KafkaConsumer, "Notification Service - Notification requested consumer"), "Received message with correlation ID {correlationId}: {topic}", messageValue.CorrelationId, result.Topic);
                                    }
                                    else
                                    {
                                        _logger.LogInformation(new EventId(NotificationLoggingIds.KafkaConsumer, "Notification Service - Notification requested consumer"), "Received message without correlation ID: {topic}", result.Topic);
                                    }

                                    _logger.LogInformation(new EventId(NotificationLoggingIds.KafkaConsumer, "Notification Service - Notification requested consumer"), "Consume Event for: A '{notificationType}' notification request was made for facility '{key}'.", messageValue.NotificationType, result.Message.Key);

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
                                                    var invalidEmailEx = new ArgumentException("Invalid email address received.");
                                                    //var pattern = @"(?<=[\w]{1})[\w-\._\+%]*(?=[\w]{1}@)";
                                                    //var maskedUsername = Regex.Replace(recipient ?? "", pattern, m => new string('*', m.Length));
                                                    invalidEmailEx.Data.Add("email-address", recipient);
                                                    _logger.LogWarning(new EventId(NotificationLoggingIds.KafkaConsumer, "Notification Service - Notification requested consumer"), invalidEmailEx, "The email addresss '{email}' is invalid. Please only use valid email addresses.", recipient);
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
                                                    var invalidEmailEx = new ArgumentException("Invalid email address received.");
                                                    invalidEmailEx.Data.Add("email-address", recipient);
                                                    _logger.LogWarning(new EventId(NotificationLoggingIds.KafkaConsumer, "Notification Service - Notification requested consumer"), invalidEmailEx, "The email addresss '{email}' is invalid. Please only use valid email addresses.", recipient);                                                   
                                                }
                                                else 
                                                { 
                                                    bccs.Add(recipient); 
                                                }
                                            }
                                        }
                                    }

                                    //create audit event
                                    CreateNotificationModel notificationModel = _notificationFactory.CreateNotificationModelCreate(messageValue.NotificationType, result.Message.Key, messageValue.CorrelationId, messageValue.Subject, messageValue.Body, recipients, bccs);                                               

                                    string notificationId = await _createNotificationCommand.Execute(notificationModel);
                                    _logger.LogInformation(new EventId(NotificationLoggingIds.KafkaConsumer, "Notification Service - Notification requested consumer"), "Successfully created new notification '{notificationId}'.", notificationId);

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
                                }

                            }, cancellationToken);

                    }
                        catch (ConsumeException ex)
                        {
                            _logger.LogCritical(new EventId(NotificationLoggingIds.KafkaConsumer, "Notification Service - Notification requested consumer"), ex, "Consumer error: {reason}", ex.Error.Reason);
                            if (ex.Error.IsFatal)
                            {
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogCritical(new EventId(NotificationLoggingIds.KafkaConsumer, "Notification Service - Notification requested consumer"), ex, "Failed to generate notification from kafka message.");
                            break;
                        }
                    }

                    _notificationRequestedConsumer.Close();
                    _notificationRequestedConsumer.Dispose();

                }
                catch (OperationCanceledException oce)
                {
                    _logger.LogCritical(new EventId(NotificationLoggingIds.KafkaConsumer, "Notification Service - Notification requested consumer"), oce, "Operation Canceled: {message}", oce.Message);
                    _notificationRequestedConsumer.Close();
                    _notificationRequestedConsumer.Dispose();
                }
            }
        }

    }
}
