using LantanaGroup.Link.Account.Application.Commands.AuditEvent;
using LantanaGroup.Link.Account.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Account.Application.Models.User;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Extensions.Telemetry;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Microsoft.AspNetCore.Identity;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.User
{
    public class UpdateUser : IUpdateUser
    {
        private readonly ILogger<CreateUser> _logger;
        private readonly UserManager<LinkUser> _userManager;
        private readonly IAccountServiceMetrics _metrics;
        private readonly ICreateAuditEvent _createAuditEvent;

        public UpdateUser(ILogger<CreateUser> logger, UserManager<LinkUser> userManager, IAccountServiceMetrics metrics, ICreateAuditEvent createAuditEvent)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _createAuditEvent = createAuditEvent ?? throw new ArgumentNullException(nameof(createAuditEvent));
        }

        public async Task<bool> Execute(ClaimsPrincipal? requestor, LinkUserModel model, CancellationToken cancellationToken = default)
        {
            //generate tags for telemetry
            List<KeyValuePair<string, object?>> tagList = [new KeyValuePair<string, object?>(DiagnosticNames.UserId, model.Id)];            
            foreach (var facility in model.Facilities)
            {
                var tag = new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facility);
            }
            using Activity? activity = ServiceActivitySource.Instance.StartActivityWithTags("UdpateUser:Execute", tagList);

            try
            { 
                var user = await _userManager.FindByIdAsync(model.Id) ?? throw new ApplicationException($"User with id {model.Id} not found");
                           
                List<PropertyChangeModel> changes = GetUserDiff(model, user);

                user.UserName = model.Username;
                user.Email = model.Email;
                user.FirstName = model.FirstName;
                user.MiddleName = model.MiddleName;
                user.LastName = model.LastName;

                if (requestor is not null)
                {
                    user.LastModifiedBy = requestor?.Claims.First(c => c.Type == "sub").Value;
                }

                await _userManager.UpdateAsync(user);

                //update user roles
                var currentRoles = await _userManager.GetRolesAsync(user);
                var addedRoles = model.Roles.Except(currentRoles);
                var removedRoles = currentRoles.Except(model.Roles);

                if(addedRoles.Any())
                {
                    await _userManager.AddToRolesAsync(user, addedRoles);                    
                }

                if(removedRoles.Any())
                {
                    await _userManager.RemoveFromRolesAsync(user, removedRoles);
                }

                //capture role changes
                if (addedRoles.Any() || removedRoles.Any())
                {
                    changes.Add(new PropertyChangeModel("Roles", string.Join(",", currentRoles), string.Join(",", model.Roles)));
                }

                //generate audit event
                var auditMessage = new AuditEventMessage
                {
                    FacilityId = model.Facilities.Count > 0 ? model.Facilities.FirstOrDefault() : string.Empty,
                    Action = AuditEventType.Update,
                    EventDate = DateTime.UtcNow,
                    UserId = user.LastModifiedBy,
                    User = requestor?.Identity?.Name ?? string.Empty,
                    Resource = typeof(LinkUser).Name,
                    PropertyChanges = changes,
                    Notes = $"New user ({user.Id}) created by '{user.CreatedBy}'."
                };

                _ = Task.Run(() => _createAuditEvent.Execute(auditMessage, cancellationToken));

                return true;
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                _logger.LogUpdateUserException(model.Id, ex.Message);
                throw;
            }
        }

        private List<PropertyChangeModel> GetUserDiff(LinkUserModel model, LinkUser user)
        {
            List<PropertyChangeModel> changes = [];

            if(user.UserName != model.Username)
            {
                changes.Add(new PropertyChangeModel("UserName", user.UserName ?? string.Empty, model.Username));
            }

            if(user.Email != model.Email)
            {
                changes.Add(new PropertyChangeModel("Email", user.Email ?? string.Empty, model.Email));
            }

            if(user.FirstName != model.FirstName)
            {
                changes.Add(new PropertyChangeModel("FirstName", user.FirstName, model.FirstName));
            }

            if(user.MiddleName != model.MiddleName)
            {
                changes.Add(new PropertyChangeModel("MiddleName", user.MiddleName ?? string.Empty, model.MiddleName ?? string.Empty));
            }

            if(user.LastName != model.LastName)
            {
                changes.Add(new PropertyChangeModel("LastName", user.LastName, model.LastName));
            }

            return changes;
        }
    }
}
