using Confluent.Kafka;
using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.Notification.Commands;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries;
using LantanaGroup.Link.Notification.Domain.Entities;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using static MongoDB.Driver.WriteConcern;

namespace LantanaGroup.Link.NotificationUnitTests
{
    [TestFixture]
    public class SendNotificationTests
    {
        private AutoMocker _mocker;
        private SendNotificationCommand _command;
        private SendNotificationModel _model;
        private Channels _channels;

        private static readonly string id = "A0BB04F0-3418-4052-B1FA-22828DE7C5F8";
        private const string subject = "Test notfication";
        private const string body = "This is a test notification.";
        private static readonly List<string> recipients = new() { "Test.User@lantanagroup.com" };
        private static readonly List<string> bcc = new() { "Test.SecretUser@lantanagroup.com" };

        [SetUp]
        public void SetUp()
        {
            #region Set Up Models

            //Channels
            _channels = new Channels()
            {
                IncludeTestMessage = true,
                TestMessage = "Test",
                SubjectTestMessage = "Test",
                Email = true
            };

            //SendNotificationModel
            _model = new SendNotificationModel()
            {
                Id = id,                
                Subject = subject,
                Message = body
            };
            _model.Recipients.AddRange(recipients);
            if (bcc is not null)
            {
                _model.Bcc = new List<string>();
                _model.Bcc.AddRange(bcc);
            }

            #endregion

            _mocker = new AutoMocker();

            _mocker.GetMock<INotificationFactory>()
                .Setup(p => p.CreateSendNotificationModel(
                    _model.Id,                    
                    _model.Recipients,
                    _model.Bcc,
                    _model.Subject,
                    _model.Message
                    ))
                .Returns(_model);

            _command = _mocker.CreateInstance<SendNotificationCommand>();

            _mocker.GetMock<IOptions<Channels>>()
                .Setup(p => p.Value)
                .Returns(_channels);

            _mocker.GetMock<IEmailService>()
                .Setup(p => p.Send(_model.Id, _model.Recipients, _model.Bcc, _model.Subject, _model.Message, null))
                .Returns(Task.FromResult<bool>(true));

            _mocker.GetMock<INotificationRepository>()
                .Setup(p => p.SetNotificationSentOnAsync(NotificationId.FromString(_model.Id), CancellationToken.None)).Returns(Task.FromResult<bool>(true));           

        }

        [Test]
        public void TestExecuteShouldSendNotification()
        {
            Task<bool> sent = _command.Execute(_model);

            _mocker.GetMock<IEmailService>().Verify(p => p.Send(_model.Id, _model.Recipients, _model.Bcc, _model.Subject, _model.Message, null), Times.Once());           

        }

        //[Test]
        //public void TestExecuteShouldUpdateNotificationSentOnProperty()
        //{
        //    Task<bool> sentOnSet = _command.Execute(_model);

        //    _mocker.GetMock<INotificationRepository>().Verify(p => p.SetNotificationSentOn(NotificationId.FromString(_model.Id)), Times.Once());

        //}

    }    
}
