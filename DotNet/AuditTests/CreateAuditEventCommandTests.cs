using LantanaGroup.Link.Audit.Application.Commands;
using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using Moq;
using Moq.AutoMock;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.AuditUnitTests
{
    [TestFixture]
    public class CreateAuditEventCommandTests
    {
        private CreateAuditEventCommand _command;
        private AutoMocker _mocker;
        private CreateAuditEventModel _model;
        private CreateAuditEventModel _getSetModel;
        private AuditLog _auditEvent;              

        private const string FacilityId = "TestFacility_001";
        private const string ServiceName = "Account Service";
        private const string CorrelationId = "MockCorrelationId_001";
        private static readonly DateTime EventDate = new DateTime(2023, 3, 20);
        private static readonly string UserId = "81db555a-2dcc-4fba-a185-0a74f025b68a";
        private const string User = "Admin Joe";
        private static readonly string Action = AuditEventType.Create.ToString();
        private const string Resource = "Account(8ddb2379-aef0-4ebc-895f-48753c412caa)";
        private static readonly List<PropertyChangeModel> PropertyChanges = new List<PropertyChangeModel>() { new PropertyChangeModel() { PropertyName = "FirstName", InitialPropertyValue = "Richard", NewPropertyValue = "Rich" } };
        private const string Notes = "";  

        [SetUp]
        public void SetUp()
        {            
            _getSetModel = new CreateAuditEventModel();

            #region Set Up Models

            //set up create audit event model
            _model = new CreateAuditEventModel()
            {
                FacilityId = FacilityId,
                ServiceName = ServiceName,
                CorrelationId = CorrelationId,
                EventDate = EventDate,
                UserId = UserId,
                User = User,
                Action = Action,
                Resource = Resource,
                PropertyChanges = PropertyChanges,
                Notes = Notes
            };                       

            //set up audit entity
            _auditEvent = new AuditLog();
            _auditEvent.AuditId = AuditId.NewId();
            _auditEvent.FacilityId = FacilityId;
            _auditEvent.ServiceName = ServiceName;
            _auditEvent.CorrelationId = CorrelationId;
            _auditEvent.EventDate = EventDate;
            _auditEvent.UserId = UserId;
            _auditEvent.User = User;
            _auditEvent.Action = Action;
            _auditEvent.Resource = Resource;
            _auditEvent.CreatedOn = DateTime.UtcNow;

            if (PropertyChanges is not null)
            {
                if (_auditEvent.PropertyChanges is not null)
                    _auditEvent.PropertyChanges.Clear();
                else
                    _auditEvent.PropertyChanges = new List<EntityPropertyChange>();

                _auditEvent.PropertyChanges.AddRange(PropertyChanges.Select(p => new EntityPropertyChange { PropertyName = p.PropertyName, InitialPropertyValue = p.InitialPropertyValue, NewPropertyValue = p.NewPropertyValue }).ToList());
            }
            _auditEvent.Notes = Notes;

            #endregion


            _mocker = new AutoMocker();

            _mocker.GetMock<IAuditFactory>()
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
                .Returns(_auditEvent);

            _command = _mocker.CreateInstance<CreateAuditEventCommand>();

            _mocker.GetMock<IAuditRepository>()
                .Setup(p => p.AddAsync(_auditEvent, CancellationToken.None)).Returns(Task.FromResult<bool>(true));

        }

        #region Get/Set Tests

        [Test]
        public void TestSetAndGetFacilityId()
        { 
            _getSetModel.FacilityId = FacilityId;

            Assert.That(_getSetModel.FacilityId, Is.EqualTo(FacilityId));
        }

        [Test]
        public void TestSetAndGetAction() 
        { 
            _getSetModel.Action = Action;

            Assert.That(_getSetModel.Action, Is.EqualTo("Create"));
        }

        #endregion


        [Test]
        public void TestExecuteShouldAddAuditEventToTheDatabase()
        {
            Task<AuditLog> _createdAuditEventId = _command.Execute(_model);            

            _mocker.GetMock<IAuditRepository>().Verify(p => p.AddAsync(_auditEvent, CancellationToken.None), Times.Once());

            Assert.That(_createdAuditEventId.Result, Is.Not.Null);

        }
    }
}
