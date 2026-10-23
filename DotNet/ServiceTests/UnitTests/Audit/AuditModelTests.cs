using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace UnitTests.Audit
{
    [Trait("Category", "UnitTests")]
    public class AuditModelTests
    {
        private const string PatientId = "patient-1";
        private const string ResourceId = "resource-1";
        private const string UserId = "user-1";

        private static AuditEventMessage MessageWithNewFields() => new()
        {
            EventDate = DateTime.UtcNow,
            ServiceName = "TestService",
            FacilityId = "TestFacility",
            Action = AuditEventType.SoftDelete,
            Resource = "TestResource",
            ResourceId = ResourceId,
            PatientId = PatientId,
            UserId = UserId,
            User = "test@example.com",
            CorrelationId = Guid.NewGuid().ToString()
        };

        [Fact]
        public void FromMessage_MapsPatientIdResourceIdAndUserId()
        {
            var model = AuditModel.FromMessage(MessageWithNewFields());

            Assert.Equal(PatientId, model.PatientId);
            Assert.Equal(ResourceId, model.ResourceId);
            Assert.Equal(UserId, model.UserId);
        }

        [Fact]
        public void FromMessage_MapsSoftDeleteToActionText()
        {
            var model = AuditModel.FromMessage(MessageWithNewFields());

            Assert.Equal("SoftDelete", model.Action);
        }

        [Fact]
        public void FromMessage_WithoutTheNewFields_LeavesThemNull()
        {
            //Audit deploys ahead of its producers, so for a window every message in
            //flight carries none of the new fields
            var message = new AuditEventMessage
            {
                EventDate = DateTime.UtcNow,
                ServiceName = "TestService",
                FacilityId = "TestFacility",
                Action = AuditEventType.Create,
                Resource = "TestResource",
                CorrelationId = Guid.NewGuid().ToString()
            };

            var model = AuditModel.FromMessage(message);

            Assert.Null(model.PatientId);
            Assert.Null(model.ResourceId);
            Assert.Null(model.UserId);
            Assert.Equal("Create", model.Action);
        }

        [Fact]
        public void ToDomain_MapsPatientIdResourceIdAndUserId()
        {
            var model = AuditModel.FromMessage(MessageWithNewFields());

            var log = AuditModel.ToDomain(model);

            Assert.Equal(PatientId, log.PatientId);
            Assert.Equal(ResourceId, log.ResourceId);
            Assert.Equal(UserId, log.UserId);
        }

        [Fact]
        public void ToDomain_WithoutTheNewFields_LeavesThemNull()
        {
            var log = AuditModel.ToDomain(new AuditModel { Resource = "TestResource" });

            Assert.Null(log.PatientId);
            Assert.Null(log.ResourceId);
            Assert.Null(log.UserId);
        }

        [Fact]
        public void FromDomain_MapsPatientIdResourceIdAndUserId()
        {
            var log = new AuditLog
            {
                AuditId = AuditId.NewId(),
                FacilityId = "TestFacility",
                ServiceName = "TestService",
                Action = "SoftDelete",
                Resource = "TestResource",
                ResourceId = ResourceId,
                PatientId = PatientId,
                UserId = UserId
            };

            var model = AuditModel.FromDomain(log);

            Assert.Equal(PatientId, model.PatientId);
            Assert.Equal(ResourceId, model.ResourceId);
            Assert.Equal(UserId, model.UserId);
        }

        [Fact]
        public void MessageToDomain_RoundTripsEveryNewField()
        {
            var message = MessageWithNewFields();

            var log = AuditModel.ToDomain(AuditModel.FromMessage(message));
            var model = AuditModel.FromDomain(log);

            Assert.Equal(message.PatientId, model.PatientId);
            Assert.Equal(message.ResourceId, model.ResourceId);
            Assert.Equal(message.UserId, model.UserId);
            Assert.Equal("SoftDelete", model.Action);
        }
    }
}
