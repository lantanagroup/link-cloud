using Confluent.Kafka;
using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Commands;
using LantanaGroup.Link.Notification.Domain.Entities;
using Moq;
using Moq.AutoMock;

namespace LantanaGroup.Link.NotificationUnitTests
{
    [TestFixture]
    internal class UpdateNotificationConfigurationTests
    {
        private AutoMocker _mocker;
        private UpdateFacilityConfigurationCommand _command;
        private UpdateFacilityConfigurationModel _model;
        private NotificationConfig _config;
        
        private static readonly string id = "A0BB04F0-3418-4052-B1FA-22828DE7C5F8";
        private const string facilityId = "TestFacility_001";
        private static readonly List<string> emailAddresses = new() { "Test.User@lantanagroup.com" };
        private static readonly List<EnabledNotification> enabledNotifications = new List<EnabledNotification>();
        private static readonly List<FacilityChannel> channels = new List<FacilityChannel>();
 
        [SetUp]
        public void SetUp()
        {
            #region Set Up Models

            enabledNotifications.Add(new EnabledNotification() { NotificationType = "TestType", Recipients = new List<string> { "Test.User@lantanagroup.com" } });

            channels.Add(new FacilityChannel() { Name = ChannelType.Email.ToString(), Enabled = true });

            //CreateFacilityConfigurationModel
            _model = new UpdateFacilityConfigurationModel()
            {
                Id = id,
                FacilityId = facilityId,
                EmailAddresses = new List<string>(),
                EnabledNotifications = new List<EnabledNotification>(),
                Channels = new List<FacilityChannel>()
            };
            _model.EmailAddresses.AddRange(emailAddresses);
            _model.EnabledNotifications.AddRange(enabledNotifications);
            _model.Channels.AddRange(channels);

            //NotificationConfig entity
            _config = new NotificationConfig
            {
                Id = NotificationConfigId.FromString(id),
                FacilityId = facilityId,
                EmailAddresses = new List<string>(),
                EnabledNotifications = new List<EnabledNotification>(),
                Channels = new List<FacilityChannel>(),
                CreatedOn = DateTime.UtcNow
            };
            _config.EmailAddresses.AddRange(emailAddresses);
            _config.EnabledNotifications.AddRange(enabledNotifications);
            _config.Channels.AddRange(channels);

            #endregion

            _mocker = new AutoMocker();

            _mocker.GetMock<INotificationConfigurationFactory>()
                .Setup(p => p.NotificationConfigEntityCreate(
                    id,
                    facilityId,
                    emailAddresses,
                    enabledNotifications,
                    channels
                    ))
                .Returns(_config);

            _command = _mocker.CreateInstance<UpdateFacilityConfigurationCommand>();

            _mocker.GetMock<INotificationConfigurationRepository>()
                .Setup(p => p.UpdateAsync(_config, CancellationToken.None)).Returns(Task.FromResult<bool>(true));

            _mocker.GetMock<IKafkaProducerFactory>()
                .Setup(p => p.CreateAuditEventProducer(false))
                .Returns(Mock.Of<IProducer<string, AuditEventMessage>>());

        }

        [Test]
        public void TestExecuteShouldUpdateExistingNotificationConfigurationInTheDatabase()
        {
            Task<bool> _createdConfigId = _command.Execute(_model, CancellationToken.None);

            _mocker.GetMock<INotificationConfigurationRepository>().Verify(p => p.UpdateAsync(_config, CancellationToken.None), Times.Once());                        

        }

    }
}
