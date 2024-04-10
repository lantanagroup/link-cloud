using Confluent.Kafka;
using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.Notification.Commands;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries;
using LantanaGroup.Link.Notification.Domain.Entities;
using Moq;
using Moq.AutoMock;

namespace LantanaGroup.Link.NotificationUnitTests
{
    [TestFixture]
    public  class CreateNotificationTests
    {
        private AutoMocker _mocker;
        private CreateNotificationCommand _command;
        private CreateNotificationModel _model;
        private NotificationEntity _entity;
        private NotificationConfigurationModel _config;

        //private static readonly string id = "A0BB04F0-3418-4052-B1FA-22828DE7C5F8";
        private const string notificaitonType = "MeasureEvaluationFailed";
        private const string facilityId = "TestFacility_001";
        private const string correlationId = "798BC5C5-FD26-499F-B74D-411FAEC4E047";
        private const string subject = "Test notfication";
        private const string body = "This is a test notification.";
        //private static readonly List<string> recipients = new() { "Test.User@lantanagroup.com" };
        private static readonly List<string> bcc = new() { "Test.SecretUser@lantanagroup.com" };
        //private static readonly List<EnabledNotification> enabledNotifications = new List<EnabledNotification>();
 
        private static readonly string configId = "A0BB04F0-3418-4052-B1FA-22828DE7C5F8";
        private static readonly List<string> emailAddresses = new() { "Test.User@lantanagroup.com" };
        private static readonly List<EnabledNotification> enabledNotifications = new List<EnabledNotification>();


        [SetUp]
        public void SetUp()
        {
            #region Set Up Models

            //NotificationConfig
            _config = new NotificationConfigurationModel()
            {
                FacilityId = facilityId,
                EmailAddresses = new List<string>(),
                EnabledNotifications = new List<EnabledNotification>()
            };
            _config.EmailAddresses.AddRange(emailAddresses);
            _config.EnabledNotifications.AddRange(enabledNotifications);
            //enabledNotifications.Add(new EnabledNotification() { NotificationType = "TestType", Recipients = new List<string> { "Test.User@lantanagroup.com" } });

            //CreateNotificationModel
            _model = new CreateNotificationModel()
            {
                NotificationType = notificaitonType,
                FacilityId = facilityId,
                CorrelationId = correlationId,
                Subject = subject,
                Body = body                
            };

            _model.Recipients.AddRange(_config.EmailAddresses);
            if (bcc is not null)
            {
                _model.Bcc = new List<string>();
                _model.Bcc.AddRange(bcc);
            }


            //Notification entity
            _entity = new NotificationEntity
            {
                Id = NotificationId.NewId(),
                NotificationType = notificaitonType,
                FacilityId = facilityId,
                CorrelationId = correlationId,
                Subject = subject,
                Body = body,                
                CreatedOn = DateTime.UtcNow                
            };
            if (emailAddresses is not null) 
            {
                _entity.Recipients = new List<string>();
                _entity.Recipients.AddRange(_config.EmailAddresses); 
            }
            if (bcc is not null) 
            { 
                _entity.Bcc = new List<string>();
                _entity.Bcc.AddRange(bcc); 
            }
            

            #endregion


            _mocker = new AutoMocker();

            _mocker.GetMock<INotificationFactory>()
                .Setup(p => p.NotificationEntityCreate(        
                    _model.NotificationType,
                    _model.FacilityId,
                    _model.CorrelationId,
                    _model.Subject,
                    _model.Body,
                    _model.Recipients,
                    _model.Bcc
                    ))
                .Returns(_entity);

            //create mock for the CreateNotificationCommand
            _command = _mocker.CreateInstance<CreateNotificationCommand>();            

            _mocker.GetMock<IGetFacilityConfigurationQuery>()
                .Setup(p => p.Execute(facilityId, CancellationToken.None))
                .Returns(Task.FromResult< NotificationConfigurationModel>(_config));

            _mocker.GetMock<INotificationRepository>()
                .Setup(p => p.UpdateAsync(_entity, CancellationToken.None)).Returns(Task.FromResult<bool>(true));

            _mocker.GetMock<IKafkaProducerFactory>()
                .Setup(p => p.CreateAuditEventProducer(false))
                .Returns(Mock.Of<IProducer<string, AuditEventMessage>>());

        }

        [Test]
        public void TestExecuteShouldAddNotificationToTheDatabase()
        {
            Task<string> _createdNotificationId = _command.Execute(_model, CancellationToken.None);

            _mocker.GetMock<INotificationRepository>().Verify(p => p.AddAsync(_entity, CancellationToken.None), Times.Once());

            Assert.That(_createdNotificationId.Result, Is.Not.Empty);
        }

    }
}
