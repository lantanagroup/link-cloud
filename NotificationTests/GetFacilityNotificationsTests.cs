using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.Notification.Queries;
using LantanaGroup.Link.Notification.Domain.Entities;
using Moq;
using Moq.AutoMock;

namespace LantanaGroup.Link.NotificationUnitTests
{
    [TestFixture]
    public class GetFacilityNotificationsTests
    {
        private AutoMocker _mocker;
        private GetFacilityNotificationsQuery _query;
        private NotificationEntity _entity;
        private List<NotificationEntity> _entities;
        private PaginationMetadata _pagedMetaData;

        private static readonly string id = "A0BB04F0-3418-4052-B1FA-22828DE7C5F8";
        private const string notificaitonType = "MeasureEvaluationFailed";
        private const string facilityId = "TestFacility_001";
        private const string correlationId = "798BC5C5-FD26-499F-B74D-411FAEC4E047";
        private const string subject = "Test notfication";
        private const string body = "This is a test notification.";
        private static readonly List<string> recipients = new() { "Test.User@lantanagroup.com" };
        private static readonly List<string> bcc = new() { "Test.SecretUser@lantanagroup.com" };

        //search parameters
        private const string searchText = null;
        private const string filterFacilityBy = null;
        private const string filterNontificationTypeBy = null;
        private static readonly DateTime? createdOnStart = null;
        private static readonly DateTime? createdOnEnd = null;
        private static readonly DateTime? sentOnStart = null;
        private static readonly DateTime? sentOnEnd = null;
        private const string sortBy = null;
        private const int pageNumber = 1;
        private const int pageSize = 10;
        private const int itemCount = 1;

        [SetUp]
        public void SetUp()
        {
            #region Set Up Models

            _entity = new NotificationEntity
            {
                Id = NotificationId.FromString(id),
                NotificationType = notificaitonType,
                FacilityId = facilityId,
                CorrelationId = correlationId,
                Subject = subject,
                Body = body,
                CreatedOn = DateTime.UtcNow,
                SentOn = new List<DateTime> { DateTime.UtcNow },
            };
            if (recipients is not null)
            {
                _entity.Recipients = new List<string>();
                _entity.Recipients.AddRange(recipients);
            }
            if (bcc is not null)
            {
                _entity.Bcc = new List<string>();
                _entity.Bcc.AddRange(bcc);
            }

            _entities = new List<NotificationEntity>
            {
                _entity
            };

            //set up paged metadata
            _pagedMetaData = new PaginationMetadata(pageSize, pageNumber, itemCount);

            #endregion

            _mocker = new AutoMocker();            

            _query = _mocker.CreateInstance<GetFacilityNotificationsQuery>();

            var output = (records: _entities, metaData: _pagedMetaData);

            _mocker.GetMock<INotificationRepository>()
                .Setup(p => p.GetFacilityNotificationsAsync(facilityId, sortBy, SortOrder.Ascending, pageSize, pageNumber, CancellationToken.None))
                .ReturnsAsync(output);
        }

        //[Test]
        //public void TestExecuteShouldReturnAllMatchingFacilityNotificationsFromTheDatabase()
        //{
        //    Task<PagedNotificationModel> results = _query.Execute(filterFacilityBy, sortBy, SortOrder.Ascending, pageSize, pageNumber);

        //    _mocker.GetMock<INotificationRepository>().Verify(p => p.GetFacilityNotifications(facilityId, sortBy, SortOrder.Ascending, pageSize, pageNumber), Times.Once());

        //}    

    }
}
