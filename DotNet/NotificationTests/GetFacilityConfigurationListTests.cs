using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries;
using LantanaGroup.Link.Notification.Domain.Entities;
using Moq.AutoMock;
using Moq;

namespace LantanaGroup.Link.NotificationUnitTests
{
    [TestFixture]
    public class GetFacilityConfigurationListTests
    {
        private AutoMocker _mocker;
        private GetFacilityConfigurationListQuery _query;
        private NotificationConfig _config;
        private List<NotificationConfig> _configs;
        private PaginationMetadata _pagedMetaData;

        private static readonly string id = "A0BB04F0-3418-4052-B1FA-22828DE7C5F8";
        private const string facilityId = "TestFacility_001";
        private static readonly List<string> emailAddresses = new() { "Test.User@lantanagroup.com" };
        private static readonly List<EnabledNotification> enabledNotifications = new List<EnabledNotification>();
        private static readonly List<FacilityChannel> channels = new List<FacilityChannel>();

        //search parameters
        private const string searchText = null;
        private const string filterFacilityBy = facilityId;
        private const string sortBy = null;
        private const int pageNumber = 1;
        private const int pageSize = 10;
        private const int itemCount = 1;

        [SetUp]
        public void SetUp()
        {
            #region Set Up Models

            enabledNotifications.Add(new EnabledNotification() { NotificationType = "TestType", Recipients = new List<string> { "Test.User@lantanagroup.com" } });

            channels.Add(new FacilityChannel() { Name = ChannelType.Email.ToString(), Enabled = true });

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

            _configs = new List<NotificationConfig>
            {
                _config
            };

            //set up paged metadata
            _pagedMetaData = new PaginationMetadata(pageSize, pageNumber, itemCount);


            #endregion

            _mocker = new AutoMocker();

            _query = _mocker.CreateInstance<GetFacilityConfigurationListQuery>();

            var output = (configs: _configs, metaData: _pagedMetaData);
            _mocker.GetMock<INotificationConfigurationRepository>()
                .Setup(p => p.SearchAsync(searchText, filterFacilityBy, sortBy, SortOrder.Ascending, pageSize, pageNumber, CancellationToken.None))
                .ReturnsAsync(output);
        }

        [Test]
        public void TestExecuteShouldReturnAllMatchingConfigurations()
        {
            Task<PagedNotificationConfigurationModel> _config = _query.Execute(searchText, filterFacilityBy, sortBy, SortOrder.Ascending, pageSize, pageNumber);

            _mocker.GetMock<INotificationConfigurationRepository>().Verify(p => p.SearchAsync(searchText, filterFacilityBy, sortBy, SortOrder.Ascending, pageSize, pageNumber, CancellationToken.None), Times.Once());

        }
    }
}
