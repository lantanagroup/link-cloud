﻿using Confluent.Kafka;
using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Commands;
using LantanaGroup.Link.Notification.Domain.Entities;
using Moq;
using Moq.AutoMock;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.NotificationUnitTests
{
    [TestFixture]
    public class CreateNotificationConfigurationTests
    {
        private AutoMocker _mocker;
        private CreateFacilityConfigurationCommand _command;
        private CreateFacilityConfigurationModel _model;
        private NotificationConfig _entity;

        private const string facilityId = "TestFacility_001";
        private static readonly List<string> emailAddresses = new() { "Test.User@lantanagroup.com" };
        private static readonly List<EnabledNotification> enabledNotifications = new List<EnabledNotification>();
        private static readonly List<FacilityChannel> channels = new List<FacilityChannel>();

        [SetUp]
        public void SetUp() 
        {
            #region Set Up Models

            enabledNotifications.Add(new EnabledNotification() { NotificationType = "TestType", Recipients = new List<string> { "Test.User@lantanagroup.com" }});

            channels.Add(new FacilityChannel() { Name = ChannelType.Email.ToString(), Enabled = true });

            //CreateFacilityConfigurationModel
            _model = new CreateFacilityConfigurationModel()
            {
                FacilityId = facilityId,
                EmailAddresses = new List<string>(),
                EnabledNotifications = new List<EnabledNotification>(),
                Channels = new List<FacilityChannel>()
            };
            _model.EmailAddresses.AddRange(emailAddresses);
            _model.EnabledNotifications.AddRange(enabledNotifications);
            _model.Channels.AddRange(channels);

            //NotificationConfig entity
            _entity = new NotificationConfig
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = facilityId,
                EmailAddresses = new List<string>(),
                EnabledNotifications = new List<EnabledNotification>(),
                Channels = new List<FacilityChannel>(),
                CreatedOn = DateTime.UtcNow
            };
            _entity.EmailAddresses.AddRange(emailAddresses);
            _entity.EnabledNotifications.AddRange(enabledNotifications);
            _entity.Channels.AddRange(channels);

            #endregion

            _mocker = new AutoMocker();

            _mocker.GetMock<INotificationConfigurationFactory>()
                .Setup(p => p.NotificationConfigEntityCreate(
                    facilityId,
                    emailAddresses,
                    enabledNotifications,
                    channels
                    ))
                .Returns(_entity);

            _command = _mocker.CreateInstance<CreateFacilityConfigurationCommand>();

            _mocker.GetMock<INotificationConfigurationRepository>()
                .Setup(p => p.AddAsync(_entity)).Returns(Task.FromResult<bool>(true));          
            
            _mocker.GetMock<IKafkaProducerFactory>()
                .Setup(p => p.CreateAuditEventProducer())
                .Returns(Mock.Of<IProducer<string, AuditEventMessage>>());

        }

        [Test]
        public void TestExecuteShouldAddNotificationConfigurationToTheDatabase()
        {
            Task<string> _createdConfigId = _command.Execute(_model);

            _mocker.GetMock<INotificationConfigurationRepository>().Verify(p => p.AddAsync(_entity), Times.Once());

            Assert.That(_createdConfigId.Result, Is.Not.Empty);

        }

    }
}
