using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.Notification.Commands;
using LantanaGroup.Link.Notification.Application.Notification.Queries;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries;
using LantanaGroup.Link.Notification.Domain.Entities;
using LantanaGroup.Link.Notification.Infrastructure;
using ServiceActivitySource = LantanaGroup.Link.Notification.Infrastructure.ServiceActivitySource;
using LantanaGroup.Link.Notification.Infrastructure.Logging;
using LantanaGroup.Link.Notification.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using System.Diagnostics;

namespace LantanaGroup.Link.Notification.Listeners
{
    public class NotificationRequestedListener : BackgroundService
    {
        private readonly ILogger<NotificationRequestedListener> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly INotificationFactory _notificationFactory;
        private readonly IKafkaConsumerFactory _kafkaConsumerFactory;
        private readonly ITransientExceptionHandler<NotificationRequestedListener, string, NotificationMessage> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<NotificationRequestedListener, string, NotificationMessage> _deadLetterExceptionHandler;

        public NotificationRequestedListener(ILogger<NotificationRequestedListener> logger, INotificationFactory notificationFactory, 
            IKafkaConsumerFactory kafkaConsumerFactory, IServiceScopeFactory scopeFactory,
            ITransientExceptionHandler<NotificationRequestedListener, string, NotificationMessage> transientExceptionHandler,
            IDeadLetterExceptionHandler<NotificationRequestedListener, string, NotificationMessage> deadLetterExceptionHandler)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));           
            _notificationFactory = notificationFactory ?? throw new ArgumentNullException(nameof(notificationFactory));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentNullException(nameof(kafkaConsumerFactory));
            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentNullException(nameof(transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentNullException(nameof(deadLetterExceptionHandler));
            _transientExceptionHandler.Topic = nameof(KafkaTopic.NotificationRequested);
            _deadLetterExceptionHandler.Topic = KafkaTopicNames.Error(nameof(KafkaTopic.NotificationRequested));
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async Task StartConsumerLoop(CancellationToken cancellationToken)
        {
            var assignmentTracker = new KafkaAssignmentTracker();
            using (var _consumer = _kafkaConsumerFactory.CreateNotificationRequestedConsumer(enableAutoCommit: false, assignmentTracker))
            {
                try
                {
                    _consumer.Subscribe(KafkaTopicNames.Subscription(nameof(KafkaTopic.NotificationRequested), NotificationConstants.ServiceName));
                    _logger.LogConsumerStarted(nameof(KafkaTopic.NotificationRequested), DateTime.UtcNow);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {                            
                            await _consumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                            {
                                var accounted = false;
                                try
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
                                    var facilityId = KafkaIdentity.Facility(messageValue.FacilityId, result.Message.Key);

                                    if (result.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
                                    {
                                        messageValue.CorrelationId = System.Text.Encoding.UTF8.GetString(headerValue);
                                    }                                   

                                    _logger.LogNotificationRequestedConsumption(messageValue.NotificationType);

                                    //remove any email addresses that are not valid
                                    List<string> recipients = new List<string>();
                                    List<string> bccs = new List<string>();

                                    //create scoped create audit event command
                                    //deals with issue of scoped services being used within singleton hosted service
                                    //that does not have any context of the scoped repository/dbContext used within the commands
                                    using (var scope = _scopeFactory.CreateScope())
                                    {
                                        var _validateEmailAddressCommand = scope.ServiceProvider.GetRequiredService<IValidateEmailAddressCommand>();
                                        var _createNotificationCommand = scope.ServiceProvider.GetRequiredService<ICreateNotificationCommand>();
                                        var _getNotificationQuery = scope.ServiceProvider.GetRequiredService<IGetNotificationQuery>();
                                        var _sendNotificationCommand = scope.ServiceProvider.GetRequiredService<ISendNotificationCommand>();
                                        var _getFacilityConfigurationQuery = scope.ServiceProvider.GetRequiredService<IGetFacilityConfigurationQuery>();

                                        using (ServiceActivitySource.Instance.StartActivity("Remove invalid email addresses from consumed message"))
                                        {
                                            if (messageValue.Recipients is not null)
                                            {                                                
                                                foreach (var recipient in messageValue.Recipients)
                                                {
                                                    bool isValid = await _validateEmailAddressCommand.Execute(recipient, consumeCancellationToken);
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
                                                    bool isValid = await _validateEmailAddressCommand.Execute(recipient, consumeCancellationToken);
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
                                        CreateNotificationModel notificationModel = _notificationFactory.CreateNotificationModelCreate(messageValue.NotificationType, facilityId, messageValue.CorrelationId, messageValue.Subject, messageValue.Body, recipients, bccs);                                                                                                       
                                                                          
                                        string notificationId = await _createNotificationCommand.Execute(notificationModel, consumeCancellationToken);
                                        _logger.LogNotificationCreation(notificationId, notificationModel);

                                        //send notification
                                        NotificationModel notification = await _getNotificationQuery.Execute(NotificationId.FromString(notificationId), consumeCancellationToken);
                                        SendNotificationModel sendModel = _notificationFactory.CreateSendNotificationModel(notification.Id, notification.Recipients, notification.Bcc, notification.Subject, notification.Body);

                                        //if a facility based notification, get their configuration and add it to the send model
                                        if (!string.IsNullOrEmpty(facilityId))
                                        {
                                            NotificationConfigurationModel config = await _getFacilityConfigurationQuery.Execute(facilityId, consumeCancellationToken);
                                            sendModel.FacilityConfig = config;
                                        }

                                        //asynchrounously send the email
                                        await _sendNotificationCommand.Execute(sendModel, consumeCancellationToken);
                                    }                                    

                                    accounted = true;
                                }
                                }
                                catch (DeadLetterException ex) when (result != null)
                                {
                                    Activity.Current?.SetStatus(ActivityStatusCode.Error);
                                    var facilityId = KafkaIdentity.Facility(result.Message?.Value?.FacilityId, result.Message?.Key);
                                    _deadLetterExceptionHandler.HandleException(result, ex, facilityId ?? string.Empty);
                                    accounted = true;
                                }
                                catch (TransientException ex) when (result != null)
                                {
                                    Activity.Current?.SetStatus(ActivityStatusCode.Error);
                                    var facilityId = KafkaIdentity.Facility(result.Message?.Value?.FacilityId, result.Message?.Key);
                                    _transientExceptionHandler.HandleException(result, ex, facilityId ?? string.Empty);
                                    accounted = true;
                                }
                                catch (OperationCanceledException) when (consumeCancellationToken.IsCancellationRequested)
                                {
                                    throw;
                                }
                                catch (Exception ex)
                                {
                                    Activity.Current?.SetStatus(ActivityStatusCode.Error);
                                    _logger.LogConsumerException(nameof(KafkaTopic.NotificationRequested), ex.Message);
                                    if (result == null)
                                    {
                                        throw;
                                    }

                                    var facilityId = KafkaIdentity.Facility(result.Message?.Value?.FacilityId, result.Message?.Key);
                                    _transientExceptionHandler.HandleException(
                                        result,
                                        new TransientException("Notification Exception thrown: " + ex.Message, ex),
                                        facilityId ?? string.Empty);
                                    accounted = true;
                                }
                                finally
                                {
                                    if (accounted && result != null && !consumeCancellationToken.IsCancellationRequested)
                                    {
                                        assignmentTracker.MarkProcessed(result);
                                        _consumer.SafeCommit(result, _logger);
                                    }
                                }

                            }, cancellationToken);

                        }
                        catch (ConsumeException ex)
                        {
                            Activity.Current?.SetStatus(ActivityStatusCode.Error);
                            _logger.LogConsumerException(nameof(KafkaTopic.NotificationRequested), ex.Message);
                            if (ex.Error.IsFatal || ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                            {
                                break;
                            }

                            var rawKey = ex.ConsumerRecord?.Message?.Key != null
                                ? System.Text.Encoding.UTF8.GetString(ex.ConsumerRecord.Message.Key)
                                : null;
                            _deadLetterExceptionHandler.HandleConsumeException(ex, KafkaIdentity.Facility(null, rawKey) ?? string.Empty);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            Activity.Current?.SetStatus(ActivityStatusCode.Error);
                            _logger.LogConsumerException(nameof(KafkaTopic.NotificationRequested), ex.Message);
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
