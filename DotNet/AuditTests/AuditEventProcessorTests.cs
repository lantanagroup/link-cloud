using Confluent.Kafka;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Application.Services;
using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Audit.Domain.Managers;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.Logging;
using Moq;

namespace AuditTests
{
    public class AuditEventProcessorTests
    {
        private readonly Mock<ILogger<AuditEventProcessor>> _logger = new();
        private readonly Mock<IAuditManager> _auditManagerMock = new();        

        [Fact]
        public async Task AuditEventProcessor_ShouldNotAcceptNullMessageResults()
        {                        
            AuditEventProcessor proccessor = new(_logger.Object, _auditManagerMock.Object);
            ConsumeResult<string, AuditEventMessage>? result = null;            

            await Assert.ThrowsAsync<DeadLetterException>(() => proccessor.ProcessAuditEvent(result, It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task AuditEventProcessor_ShouldNotAcceptNullMessageValues()
        {
            AuditEventProcessor proccessor = new(_logger.Object, _auditManagerMock.Object);
            ConsumeResult<string, AuditEventMessage>? result = new()
            {
                Message = new Message<string, AuditEventMessage>() 
                { 
                    Value = null,  
                    Headers = new Headers(),
                    Key = "TestKey"
                }
            };

            await Assert.ThrowsAsync<DeadLetterException>(() => proccessor.ProcessAuditEvent(result, It.IsAny<CancellationToken>()));

        }        

        [Fact]
        public async Task AuditEventProcessor_CanCreateAnAuditEvent()
        {
            //set up message
            AuditEventMessage message = new()
            {
                EventDate = DateTime.UtcNow,
                ServiceName = "TestService",
                FacilityId = "TestFacility",
                Action = AuditEventType.Create,
                Resource = "TestResource",
                CorrelationId = Guid.NewGuid().ToString()
            };

            //create consume result
            ConsumeResult<string, AuditEventMessage>? result = new()
            {
                Message = new Message<string, AuditEventMessage>()
                {
                    Value = message,
                    Headers = new Headers(),
                    Key = "TestKey"
                }
            };

            _auditManagerMock.Setup(x => x.CreateAuditLog(It.IsAny<AuditModel>(), default)).ReturnsAsync(new AuditLog());
            AuditEventProcessor proccessor = new(_logger.Object, _auditManagerMock.Object);
            var outcome = await proccessor.ProcessAuditEvent(result, It.IsAny<CancellationToken>());            

            Assert.True(outcome);
        }
    }
}
