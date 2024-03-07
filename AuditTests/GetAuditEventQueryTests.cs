using LantanaGroup.Link.Audit.Application.Audit.Queries;
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
    public class GetAuditEventQueryTests
    {
        private AutoMocker _mocker;
        private GetAuditEventQuery _query;
        private AuditLog _auditEvent;

        private static readonly string _auditId = "aa7d82c3-8ca0-47b2-8e9f-c2b4c3baf856";

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

            #region Set Up Models

            //set up audit entity
            _auditEvent = new AuditLog();
            _auditEvent.Id = AuditId.FromString(_auditId);
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

            _query = _mocker.CreateInstance<GetAuditEventQuery>();

            _mocker.GetMock<IAuditRepository>()
                .Setup(p => p.GetAsync(AuditId.FromString(_auditId), true, CancellationToken.None)).Returns(Task.FromResult<AuditLog?>(_auditEvent));
   
        }

        [Test]
        public void TestExecuteShouldReturnAnAuditEventFromTheDatabase()
        {
            Task<AuditModel> _foundAuditEvent = _query.Execute(AuditId.FromString(_auditId));

            _mocker.GetMock<IAuditRepository>().Verify(p => p.GetAsync(AuditId.FromString(_auditId), true, CancellationToken.None), Times.Once());
               
        }
    
    }
}
