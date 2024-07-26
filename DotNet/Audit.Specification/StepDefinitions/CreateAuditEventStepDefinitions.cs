using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Audit.Domain.Managers;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Moq;
using Moq.AutoMock;
using NUnit.Framework;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Audit.Specification.StepDefinitions
{
    [Binding]    
    public class CreateAuditEventStepDefinitions
    {
        private AutoMocker mocker;
        private AuditEventMessage auditMessage;
        private AuditManager manager;        
        private AuditId createdAuditEventId = AuditId.Empty;
        private AuditModel model;
        private AuditLog auditEntity;

        private static readonly string Id = new Guid("aa7d82c3-8ca0-47b2-8e9f-c2b4c3baf856").ToString();
        private const string FacilityId = "TestFacility_001";
        private const string ServiceName = "Account Service";
        private const string CorrelationId = "490585-27437-495875";
        private static readonly DateTime EventDate = DateTime.UtcNow;
        private static readonly string UserId = "81db555a-2dcc-4fba-a185-0a74f025b68a";
        private const string User = "Admin Joe";
        private static readonly string Action = AuditEventType.Create.ToString();
        private const string Resource = "Account(8ddb2379-aef0-4ebc-895f-48753c412caa)";
        private static readonly List<PropertyChangeModel> PropertyChanges = new List<PropertyChangeModel>() { new PropertyChangeModel() { PropertyName = "FirstName", InitialPropertyValue = "Richard", NewPropertyValue = "Rich" } };
        private const string Notes = "";
        private static readonly string _emptyId = string.Empty;

       
        public CreateAuditEventStepDefinitions()
        {

            #region Set Up Models

            auditMessage = new AuditEventMessage();               
            auditMessage.FacilityId = FacilityId;
            auditMessage.ServiceName = ServiceName;
            auditMessage.CorrelationId = CorrelationId;
            auditMessage.EventDate = EventDate;
            auditMessage.UserId = UserId;
            auditMessage.User = User;
            auditMessage.Action = AuditEventType.Create;
            auditMessage.Resource = Resource;

            if (PropertyChanges is not null)
            {
                if (auditMessage.PropertyChanges is not null)
                    auditMessage.PropertyChanges.Clear();
                else
                    auditMessage.PropertyChanges = [];

                auditMessage.PropertyChanges.AddRange(PropertyChanges.Select(p => new PropertyChangeModel { PropertyName = p.PropertyName, InitialPropertyValue = p.InitialPropertyValue, NewPropertyValue = p.NewPropertyValue }).ToList());
            }
            auditMessage.Notes = Notes;            

            #endregion

            mocker = new AutoMocker();     

            manager = mocker.CreateInstance<AuditManager>();
            
            mocker.GetMock<IAuditManager>()
                .Setup(m => m.CreateAuditLog(It.IsAny<AuditModel>(), CancellationToken.None)).Returns(It.IsAny<Task<AuditLog>>);

            mocker.GetMock<IAuditRepository>()
                .Setup(p => p.AddAsync(It.IsAny<AuditLog>(), CancellationToken.None)).Returns(Task.FromResult<bool>(true));

        }

        [Given(@"I have received an AuditEventMessage")]
        public void GivenIHaveReceivedAnAuditEventMessage()
        {
            model = AuditModel.FromMessage(auditMessage);            
        }

        [When(@"I create a new AuditEvent based on this AuditEventMessage")]
        public void WhenISetTheIdPropertyTo()
        {
            Task<AuditLog> outcome = manager.CreateAuditLog(model);
            auditEntity = outcome.Result;
            createdAuditEventId = auditEntity.AuditId;

        }

        [Then(@"the Id property should be a valid unique identifier")]
        public void ThenTheIdPropertyShouldBe()
        {
            Assert.That(createdAuditEventId.Value, Is.Not.Empty);
            Assert.That(createdAuditEventId.Value, Is.Not.EqualTo(_emptyId));
        }

        [Then(@"the other properties of the AuditEvent should match those of the received AuditEventMessage")]
        public void ThenTheAuditEventPropertiesShouldMatchTheReceivedAuditMessage()
        {
            Assert.That(auditMessage.ServiceName, Is.Not.Null);
            Assert.That(auditEntity.ServiceName, Is.Not.Null);
            Assert.That(auditMessage.ServiceName!.Equals(auditEntity.ServiceName));
        }

    }
}
