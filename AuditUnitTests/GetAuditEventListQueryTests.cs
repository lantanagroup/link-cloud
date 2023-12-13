using Hl7.FhirPath.Sprache;
using LantanaGroup.Link.Audit.Application.Audit.Queries;
using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using Moq;
using Moq.AutoMock;

namespace LantanaGroup.Link.AuditUnitTests
{
    [TestFixture]
    public class GetAuditEventListQueryTests
    {
        //See the following for ideas about dealing with this type of unit testing https://www.c-sharpcorner.com/blogs/how-to-mock-mongodb-sdk-cursormethods-for-unit-testing-dal-in-c-sharp

        private AutoMocker _mocker;
        private GetAuditEventListQuery _query;
        private AuditEntity _auditEvent;
        private List<AuditEntity> _auditEvents;
        private PaginationMetadata _pagedMetaData;

        private static readonly string _auditId = new Guid("aa7d82c3-8ca0-47b2-8e9f-c2b4c3baf856").ToString();
        private const string FacilityId = "TestFacility_001";
        private const string ServiceName = "Account Service";
        private const string CorrelationId = "MockCorrelationId_001";
        private static readonly DateTime EventDate = new DateTime(2023, 3, 20);
        private const string UserId = "81db555a-2dcc-4fba-a185-0a74f025b68a";
        private const string User = "Admin Joe";
        private static readonly string Action = AuditEventType.Create.ToString();
        private const string Resource = "Account(8ddb2379-aef0-4ebc-895f-48753c412caa)";
        private static readonly List<PropertyChangeModel> PropertyChanges = new List<PropertyChangeModel>() { new PropertyChangeModel() { PropertyName = "FirstName", InitialPropertyValue = "Richard", NewPropertyValue = "Rich" } };
        private const string Notes = "";

        

        //search parameters
        private const string searchText = null;
        private const string filterFacilityBy = null;
        private const string filterCorrelationBy = null; 
        private const string filterServiceBy = null;
        private const string filterActionBy = null;
        private const string filterUserBy = UserId;
        private const string sortBy = null;
        private const int pageNumber = 1;
        private const int pageSize = 10;
        private const int itemCount = 1;


        [SetUp]
        public void SetUp()
        {

            #region Set Up Models

            //set up audit entity
            _auditEvent = new AuditEntity();
            _auditEvent.Id = _auditId;
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

            _auditEvents = new List<AuditEntity>
            {
                _auditEvent
            };

            //set up paged metadata
            _pagedMetaData = new PaginationMetadata(pageSize, pageNumber, itemCount);

            #endregion

            _mocker = new AutoMocker();

            _query = _mocker.CreateInstance<GetAuditEventListQuery>();

            var output = (auditEvents: _auditEvents, metaData: _pagedMetaData);

            _mocker.GetMock<IAuditRepository>()
                .Setup(p => p.FindAsync(searchText, filterFacilityBy, filterCorrelationBy, filterServiceBy, filterActionBy, filterUserBy, sortBy, pageSize, pageNumber))
                .ReturnsAsync(output);                
        }               

        [Test]
        public void TestExecuteShouldReturnAnAuditEventFromTheDatabase()
        {
            Task<PagedAuditModel> _foundAuditEvents = _query.Execute(searchText, filterFacilityBy, filterCorrelationBy, filterServiceBy, filterActionBy, filterUserBy, sortBy, pageSize, pageNumber);

            _mocker.GetMock<IAuditRepository>().Verify(p => p.FindAsync(searchText, filterFacilityBy, filterCorrelationBy, filterServiceBy, filterActionBy, filterUserBy, sortBy, pageSize, pageNumber), Times.Once());

        }

    }
}
