using Grpc.Core;
using LantanaGroup.Link.Audit.Application.Commands;
using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Protos;
using LantanaGroup.Link.Shared.Application.Models;
using static LantanaGroup.Link.Audit.Settings.AuditConstants;
using AuditEventMessage = LantanaGroup.Link.Audit.Protos.AuditEventMessage;

namespace LantanaGroup.Link.Audit.Presentation.Services
{
    public class AuditService : Protos.AuditService.AuditServiceBase
    {
        private readonly ILogger<AuditService> _logger;
        private readonly IAuditFactory _auditFactory;
        private readonly ICreateAuditEventCommand _createAuditEventCommand;

        public AuditService(ILogger<AuditService> logger, IAuditFactory auditFactory, ICreateAuditEventCommand createAuditEventCommand)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _auditFactory = auditFactory ?? throw new ArgumentNullException(nameof(auditFactory));
            _createAuditEventCommand = createAuditEventCommand ?? throw new ArgumentNullException(nameof(_createAuditEventCommand));
        }

        public override async Task<CreateAuditEventReply> CreateAuditEvent(AuditEventMessage model, ServerCallContext context)
        {
            try 
            {
                //Create audit event
                CreateAuditEventModel auditEvent = _auditFactory.Create(
                    model.FacilityId,
                    model.ServiceName,
                    model.CorrelationId,
                    model.EventDate == null ? null : Convert.ToDateTime(model.EventDate),
                    model.UserId == null ? null : model.UserId,
                    model.User,
                    (AuditEventType)(Convert.ToInt32(model.Action)),
                    model.Resource,
                    model.PropertyChanges?.Select(p => new PropertyChangeModel { PropertyName = p.PropertyName, InitialPropertyValue = p.InitialPropertyValue, NewPropertyValue = p.NewPropertyValue }).ToList(),
                    model.Notes);

                string auditEventId = await _createAuditEventCommand.Execute(auditEvent);

                CreateAuditEventReply response = new CreateAuditEventReply();
                response.Id = auditEventId;
                response.Message = "The audit event was created succcessfully.";

                return response;
            }
            catch (Exception ex) 
            {
                _logger.LogError(AuditLoggingIds.GenerateItems, ex, $"Failed to create a new audit event from grpc request {model} with context {context}.");
                throw new ApplicationException($"Failed to create a new audit event from grpc request.");
            }           

        }

    }
}
