using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.Notification.Commands;
using LantanaGroup.Link.Notification.Domain.Entities;
using LantanaGroup.Link.Notification.Infrastructure;
using System.Diagnostics;

namespace LantanaGroup.Link.Notification.Application.Factory
{
    public class NotificationFactory : INotificationFactory
    {
        public CreateNotificationModel CreateNotificationModelCreate(string notificationType, string? facilityId, string? correlationId, string subject, string body, List<string> recipients, List<string>? bcc)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Factory - Create NotificationModel[Create]");

            CreateNotificationModel model = new()
            {
                NotificationType = notificationType,
                FacilityId = facilityId,
                CorrelationId = correlationId,
                Subject = subject,
                Body = body               
            };

            if (recipients is not null)
            {
                if (model.Recipients is not null)
                    model.Recipients.Clear();
                else
                    model.Recipients = new List<string>();

                model.Recipients.AddRange(recipients);
            }

            if (bcc is not null)
            {
                if (model.Bcc is not null)
                    model.Bcc.Clear();
                else
                    model.Bcc = new List<string>();

                model.Bcc.AddRange(bcc);
            }

            return model;
        }

        public NotificationEntity NotificationEntityCreate(string notificationType, string? facilityId, string? correlationId, string subject, string body, List<string> recipients, List<string>? bcc)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Factory - Create NotificationEntity");

            NotificationEntity model = new()
            {
                Id = Guid.NewGuid().ToString(),
                NotificationType = notificationType,
                FacilityId = facilityId,
                CorrelationId = correlationId,
                Subject = subject,
                Body = body,
                CreatedOn= DateTime.UtcNow                
            };

            if (recipients is not null)
            {
                if (model.Recipients is not null)
                    model.Recipients.Clear();
                else
                    model.Recipients = new List<string>();

                model.Recipients.AddRange(recipients);
            }

            if (bcc is not null)
            {
                if (model.Bcc is not null)
                    model.Bcc.Clear();
                else
                    model.Bcc = new List<string>();

                model.Bcc.AddRange(bcc);
            }

            return model;
        }

        public NotificationModel NotificationModelCreate(string id, string notificationType, string? facilityId, string? correlationId, string subject, string body, List<string> recipients, List<string>? bcc, DateTime createdOn, DateTime? sentOn)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Factory - Create NotificationModel");

            NotificationModel model = new()
            {
                Id = id,
                NotificationType = notificationType,
                FacilityId = facilityId,
                CorrelationId = correlationId,
                Subject = subject,
                Body = body,
                CreatedOn = createdOn,
                SentOn = sentOn
            };

            if (recipients is not null)
            {
                if (model.Recipients is not null)
                    model.Recipients.Clear();
                else
                    model.Recipients = new List<string>();

                model.Recipients.AddRange(recipients);
            }

            if (bcc is not null)
            {
                if (model.Bcc is not null)
                    model.Bcc.Clear();
                else
                    model.Bcc = new List<string>();

                model.Bcc.AddRange(bcc);
            }

            return model;
        }

        public SendNotificationModel CreateSendNotificationModel(string id, List<string> recipients, List<string>? bcc, string subject, string message)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Factory - Create SendNotificationModel");

            SendNotificationModel model = new()
            {
                Id = id,
                Subject = subject,
                Message = message
            };

            if (recipients is not null)
            {
                if (model.Recipients is not null)
                    model.Recipients.Clear();
                else
                    model.Recipients = new List<string>();

                model.Recipients.AddRange(recipients);
            }

            if (bcc is not null)
            {
                if (model.Bcc is not null)
                    model.Bcc.Clear();
                else
                    model.Bcc = new List<string>();

                model.Bcc.AddRange(bcc);
            }

            return model;
        }
    }
}
