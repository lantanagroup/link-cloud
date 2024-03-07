using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries;
using LantanaGroup.Link.Notification.Domain.Entities;
using Moq.AutoMock;
using Moq;

namespace LantanaGroup.Link.NotificationUnitTests
{
    [TestFixture]
    internal class GetNotificationConfigurationTests
    {
        private AutoMocker _mocker;
        private GetNotificationConfigurationQuery _query;
        private NotificationConfigurationModel _model;
        private NotificationConfig _config;

        private static readonly string id = "A0BB04F0-3418-4052-B1FA-22828DE7C5F8";
        private const string facilityId = "TestFacility_001";
        private static readonly List<string> emailAddresses = new() { "Test.User@lantanagroup.com" };
        private static readonly List<EnabledNotification> enabledNotifications = new List<EnabledNotification>();

        [SetUp]
        public void SetUp()
        {
            enabledNotifications.Add(new EnabledNotification() { NotificationType = "TestType", Recipients = new List<string> { "Test.User@lantanagroup.com" } });

            #region Set Up Models

            enabledNotifications.Add(new EnabledNotification() { NotificationType = "TestType", Recipients = new List<string> { "Test.User@lantanagroup.com" } });

            //CreateFacilityConfigurationModel
            _model = new NotificationConfigurationModel()
            {
                FacilityId = facilityId,
                EmailAddresses = new List<string>(),
                EnabledNotifications = new List<EnabledNotification>()
            };
            _model.EmailAddresses.AddRange(emailAddresses);
            _model.EnabledNotifications.AddRange(enabledNotifications);

            //NotificationConfig entity
            _config = new NotificationConfig
            {
                Id = NotificationConfigId.FromString(id),
                FacilityId = facilityId,
                EmailAddresses = new List<string>(),
                EnabledNotifications = new List<EnabledNotification>(),
                CreatedOn = DateTime.UtcNow
            };
            _config.EmailAddresses.AddRange(emailAddresses);
            _config.EnabledNotifications.AddRange(enabledNotifications);

            #endregion

            _mocker = new AutoMocker();

            _query = _mocker.CreateInstance<GetNotificationConfigurationQuery>();

            _mocker.GetMock<INotificationConfigurationRepository>()
                .Setup(p => p.GetAsync(NotificationConfigId.FromString(id), true, CancellationToken.None)).Returns(Task.FromResult<NotificationConfig?>(_config));

        }

        [Test]
        public void TestExecuteShouldReturnANotificationWithAMatchingId()
        {
            Task<NotificationConfigurationModel> _config = _query.Execute(NotificationConfigId.FromString(id), CancellationToken.None);

            _mocker.GetMock<INotificationConfigurationRepository>().Verify(p => p.GetAsync(NotificationConfigId.FromString(id), true, CancellationToken.None), Times.Once());

        }
    }
}
