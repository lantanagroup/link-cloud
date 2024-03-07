using LantanaGroup.Link.Audit.Application.Commands;
using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models;
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
        private CreateAuditEventModel createAuditEventModel;
        private CreateAuditEventCommand command;
        private AuditId createdAuditEventId = AuditId.Empty;
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
            createAuditEventModel = new CreateAuditEventModel(); 

            //set up audit entity
            auditEntity = new AuditLog();
            auditEntity.Id = AuditId.NewId();
            auditEntity.FacilityId = FacilityId;
            auditEntity.ServiceName = ServiceName;
            auditEntity.CorrelationId = CorrelationId;
            auditEntity.EventDate = EventDate;
            auditEntity.UserId = UserId;
            auditEntity.User = User;
            auditEntity.Action = Action;
            auditEntity.Resource = Resource;
            auditEntity.CreatedOn = DateTime.UtcNow;

            if (PropertyChanges is not null)
            {
                if (auditEntity.PropertyChanges is not null)
                    auditEntity.PropertyChanges.Clear();
                else
                    auditEntity.PropertyChanges = new List<EntityPropertyChange>();

                auditEntity.PropertyChanges.AddRange(PropertyChanges.Select(p => new EntityPropertyChange { PropertyName = p.PropertyName, InitialPropertyValue = p.InitialPropertyValue, NewPropertyValue = p.NewPropertyValue }).ToList());
            }
            auditEntity.Notes = Notes;

            #endregion

            mocker = new AutoMocker();                

            command = mocker.CreateInstance<CreateAuditEventCommand>();

            mocker.GetMock<IAuditFactory>()
                .Setup(p => p.Create(
                    FacilityId,
                    ServiceName,
                    CorrelationId,
                    EventDate,
                    UserId,
                    User,
                    Action,
                    Resource,
                    PropertyChanges,
                Notes))
                .Returns(auditEntity);

            mocker.GetMock<IAuditRepository>()
                .Setup(p => p.AddAsync(auditEntity, CancellationToken.None)).Returns(Task.FromResult<bool>(true));

        }

        [Given(@"I have received an AuditEventMessage")]
        public void GivenIHaveReceivedAnAuditEventMessage()
        {
            auditMessage = new AuditEventMessage();
            auditMessage.FacilityId = FacilityId;
            auditMessage.ServiceName = ServiceName;
            auditMessage.CorrelationId = CorrelationId;
            auditMessage.EventDate = EventDate;
            auditMessage.UserId = UserId;
            auditMessage.User = User;
            auditMessage.Action = AuditEventType.Create;
            auditMessage.Resource = Resource;
            auditMessage.PropertyChanges = PropertyChanges;
            auditMessage.Notes = Notes;            
        }

        [When(@"I create a new AuditEvent based on this AuditEventMessage")]
        public void WhenISetTheIdPropertyTo()
        {
            createAuditEventModel = new CreateAuditEventModel();
            createAuditEventModel.FacilityId = auditMessage.FacilityId;
            createAuditEventModel.ServiceName = auditMessage.ServiceName;
            createAuditEventModel.CorrelationId = auditMessage.CorrelationId;
            createAuditEventModel.EventDate = auditMessage.EventDate;
            createAuditEventModel.UserId = auditMessage.UserId;
            createAuditEventModel.User = auditMessage.User;
            createAuditEventModel.Action = auditMessage.Action.ToString();
            createAuditEventModel.Resource = auditMessage.Resource;


            if (auditMessage.PropertyChanges is not null)
            {
                if (createAuditEventModel.PropertyChanges is not null)
                    createAuditEventModel.PropertyChanges.Clear();
                else
                    createAuditEventModel.PropertyChanges = new List<PropertyChangeModel>();

                createAuditEventModel.PropertyChanges.AddRange(auditMessage.PropertyChanges);
            }

            createAuditEventModel.Notes = auditMessage.Notes;


            Task<AuditLog> outcome = command.Execute(createAuditEventModel);

            createdAuditEventId = outcome.Result.Id;

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
