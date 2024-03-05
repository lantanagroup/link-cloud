using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.Notification.Queries;
using LantanaGroup.Link.Notification.Domain.Entities;
using Moq.AutoMock;
using Moq;

namespace LantanaGroup.Link.NotificationUnitTests
{
    [TestFixture]
    public  class GetNotificationTests
    {
        private AutoMocker _mocker;
        private GetNotificationQuery _query;
        private NotificationEntity _entity;
        private NotificationModel _model;

        private static readonly string id = "A0BB04F0-3418-4052-B1FA-22828DE7C5F8";
        private const string notificaitonType = "MeasureEvaluationFailed";
        private const string facilityId = "TestFacility_001";
        private const string correlationId = "798BC5C5-FD26-499F-B74D-411FAEC4E047";
        private const string subject = "Test notfication";
        private const string body = "This is a test notification.";
        private static readonly List<string> recipients = new() { "Test.User@lantanagroup.com" };
        private static readonly List<string> bcc = new() { "Test.SecretUser@lantanagroup.com" };

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
                CreatedOn = DateTime.UtcNow
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

            _model = new NotificationModel
            {
                Id = id,
                NotificationType = notificaitonType,
                FacilityId = facilityId,
                CorrelationId = correlationId,
                Subject = subject,
                Body = body,
                CreatedOn = DateTime.UtcNow
            };
            if (recipients is not null)
            {
                _model.Recipients = new List<string>();
                _model.Recipients.AddRange(recipients);
            }
            if (bcc is not null)
            {
                _model.Bcc = new List<string>();
                _model.Bcc.AddRange(bcc);
            }

            #endregion

            _mocker = new AutoMocker();

            _query = _mocker.CreateInstance<GetNotificationQuery>();            

            _mocker.GetMock<INotificationRepository>()
                .Setup(p => p.GetAsync(NotificationId.FromString(id), true, CancellationToken.None))
                .ReturnsAsync(_entity);
        }

        [Test]
        public void TestExecuteShouldReturnANotificationWithIdFromTheDatabase()
        {
            Task<NotificationModel> results = _query.Execute(NotificationId.FromString(id), CancellationToken.None);

            _mocker.GetMock<INotificationRepository>().Verify(p => p.GetAsync(NotificationId.FromString(id), true, CancellationToken.None), Times.Once());

        }
    }
}
