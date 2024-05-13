using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using MongoDB.Driver;
using Moq;

namespace LantanaGroup.Link.AuditUnitTests
{
    [TestFixture]
    public class AuditMongoRepoTests
    {        
        private Mock<IMongoClient> mongoClient;
        private Mock<IMongoDatabase> mongodb;
        private Mock<IMongoCollection<AuditLog>> auditEntityCollection;
        private string auditCollectionName = "AuditEvents";
        private List<AuditLog> auditEntityList;
        private Mock<IAsyncCursor<AuditLog>> auditEntityCursor;

        private static readonly string _auditId ="aa7d82c3-8ca0-47b2-8e9f-c2b4c3baf856";
        private const string FacilityId = "TestFacility_001";
        private const string ServiceName = "Account Service";
        private const string CorrelationId = "MockCorrelationId_001";
        private static readonly DateTime EventDate = new DateTime(2023, 3, 20);
        private static readonly string UserId = "81db555a-2dcc-4fba-a185-0a74f025b68a";
        private const string User = "Admin Joe";
        private static readonly string Action = AuditEventType.Create.ToString();
        private const string Resource = "Account(8ddb2379-aef0-4ebc-895f-48753c412caa)";
        private static readonly List<PropertyChangeModel> PropertyChanges = [new PropertyChangeModel() { PropertyName = "FirstName", InitialPropertyValue = "Richard", NewPropertyValue = "Rich" }];
        private const string Notes = "";

        [SetUp]
        public void SetUp() 
        {
            this.mongoClient = new Mock<IMongoClient>();
            this.auditEntityCollection = new Mock<IMongoCollection<AuditLog>>();
            this.mongodb = new Mock<IMongoDatabase>();
            this.auditEntityCursor = new Mock<IAsyncCursor<AuditLog>>();

            //create audit entities
            AuditLog _auditEvent = new AuditLog();
            #region Setup for _auditEvent
            _auditEvent.AuditId = AuditId.FromString(_auditId);
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

            auditEntityList = new List<AuditLog>
            {
                _auditEvent
            };

        }

        [Test]
        public void TestAddAuditEntity() {
            var auditEntity = new AuditLog();
            auditEntity.AuditId = AuditId.NewId();

            InitializeMongoAuditEventCollection();
            //TODO How to make mongo repo more testable
            //var auditMongRepo = new AuditMongoRepo(???, new ILogger<AuditMongoRepo> logger);
            //var response = await mongoDBService.
        }

        private void InitializeMongoDb()
        {
            mongodb.Setup(x => x.GetCollection<AuditLog>(auditCollectionName,
                default)).Returns(auditEntityCollection.Object);
            mongoClient.Setup(x => x.GetDatabase(It.IsAny<string>(),
                default)).Returns(mongodb.Object);
        }

        private void InitializeMongoAuditEventCollection()
        {
            auditEntityCursor.Setup(x => x.Current).Returns(this.auditEntityList);
            auditEntityCursor.SetupSequence(x => x.MoveNext(It.IsAny<CancellationToken>())).Returns(true).Returns(false);
            auditEntityCursor.SetupSequence(x => x.MoveNextAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(true)).Returns(Task.FromResult(false));
            auditEntityCollection.Setup(x => x.AggregateAsync(It.IsAny<PipelineDefinition<AuditLog, AuditLog>>(), It.IsAny<AggregateOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(this.auditEntityCursor.Object);
            InitializeMongoDb();
        }
    }
}
