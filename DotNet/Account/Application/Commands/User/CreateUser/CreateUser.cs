using LantanaGroup.Link.Account.Application.Commands.AuditEvent;
using LantanaGroup.Link.Account.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Account.Application.Models.User;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.AspNetCore.Identity;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.User
{
    public class CreateUser : ICreateUser
    {
        private readonly ILogger<CreateUser> _logger;
        private readonly UserManager<LinkUser> _userManager;
        private readonly IAccountServiceMetrics _metrics;
        private readonly ICreateAuditEvent _createAuditEvent;

        public CreateUser(ILogger<CreateUser> logger, UserManager<LinkUser> userManager, IAccountServiceMetrics metrics, ICreateAuditEvent createAuditEvent)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _createAuditEvent = createAuditEvent ?? throw new ArgumentNullException(nameof(createAuditEvent));
        }

        public async Task<LinkUserModel> Execute(ClaimsPrincipal? requestor, LinkUserModel model, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("CreateUser:Execute");

            try
            {
                var user = new LinkUser
                {
                    FirstName = model.FirstName,
                    MiddleName = model.MiddleName,
                    LastName = model.LastName,
                    UserName = model.Username,
                    Email = model.Email,
                    Facilities = model.Facilities
                };

                //add created by if requestor is provided
                if (requestor is not null)
                {
                    user.CreatedBy = requestor.Claims.First(c => c.Type == "sub").Value;
                }

                IdentityResult? result = await _userManager.CreateAsync(user);
                if (!result.Succeeded)
                {
                    throw new ApplicationException($"Unable to create user: {result.Errors}");
                }

                _logger.LogUserCreated(user.Id, user.CreatedBy ?? "Unknown");

                //Increment the account added counter
                _metrics.IncrementAccountAddedCounter([]);

                //Add any roles provided
                if (model.Roles is not null)
                {
                    foreach (var role in model.Roles)
                    {
                        result = await _userManager.AddToRoleAsync(user, role);
                        if (!result.Succeeded)
                        {                           
                            _logger.LogUserRoleAssignmentException(user.Id, role, $"{result.Errors}");
                        }
                        _logger.LogUserAddedToRole(user.Id, role, user.CreatedBy ?? "Unknown");
                    }
                }

                model.Id = user.Id;

                //generate audit event
                var auditMessage = new AuditEventMessage
                {                    
                    FacilityId = model.Facilities.Count > 0 ? model.Facilities.FirstOrDefault() : string.Empty,
                    Action = AuditEventType.Create,
                    EventDate = DateTime.UtcNow,
                    UserId = user.CreatedBy,
                    User = requestor?.Identity?.Name ?? string.Empty,
                    Resource = typeof(LinkUser).Name,
                    Notes = $"New user ({user.Id}) created by '{user.CreatedBy}'."
                };

                _ = Task.Run(() => _createAuditEvent.Execute(auditMessage, cancellationToken));

                return model;
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                _logger.LogUserCreationException(ex.Message);
                throw;
            }            
        }
    }
}
