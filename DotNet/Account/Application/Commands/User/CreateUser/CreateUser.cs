using LantanaGroup.Link.Account.Application.Commands.AuditEvent;
using LantanaGroup.Link.Account.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Application.Models.User;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using System.Diagnostics;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.User
{
    public class CreateUser : ICreateUser
    {
        private readonly ILogger<CreateUser> _logger;
        private readonly IUserRepository _userRepository;
        private readonly IAccountServiceMetrics _metrics;
        private readonly ICreateAuditEvent _createAuditEvent;
        private readonly IRoleRepository _roleRepository;

        public CreateUser(ILogger<CreateUser> logger, IUserRepository userRepository, IAccountServiceMetrics metrics, ICreateAuditEvent createAuditEvent, IRoleRepository roleRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _createAuditEvent = createAuditEvent ?? throw new ArgumentNullException(nameof(createAuditEvent));
            _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
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

                var result = await _userRepository.CreateAsync(user, cancellationToken);
                if (!result)
                {
                    throw new ApplicationException($"Unable to create user.");
                }

                _logger.LogUserCreated(user.Id, user.CreatedBy ?? "Unknown");

                //Increment the account added counter
                _metrics.IncrementAccountAddedCounter([]);

                //Add any roles provided
                if (model.Roles is not null)
                {                   
                    foreach (var role in model.Roles)
                    {
                        //check if role exists
                        var linkRole = await _roleRepository.GetRoleByNameAsync(role, cancellationToken: cancellationToken);
                        
                        if (linkRole is null)
                        {
                            _logger.LogRoleNotFound(role);
                            continue;
                        }

                        result = await _userRepository.AddRoleAsync(user.Id, linkRole, cancellationToken);
                        if (!result)
                        {
                            _logger.LogUserRoleAssignmentException(user.Id, role, "Failed to add role.");
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
            catch (Exception)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                throw;
            }            
        }
    }
}
