﻿using LantanaGroup.Link.Account.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Extensions.Telemetry;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Microsoft.AspNetCore.Identity;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Security.Claims;
using Link.Authorization.Infrastructure;
using LantanaGroup.Link.Account.Application.Models.User;
using LantanaGroup.Link.Account.Application.Interfaces.Factories.User;
using LantanaGroup.Link.Account.Application.Commands.AuditEvent;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Account.Application.Commands.User
{
    public class RecoverUser : IRecoverUser
    {
        private readonly ILogger<DeactivateUser> _logger;
        private readonly UserManager<LinkUser> _userManager;
        private readonly IAccountServiceMetrics _metrics;
        private readonly ILinkUserModelFactory  _linkUserModelFactory;
        private readonly ICreateAuditEvent _createAuditEvent;

        public RecoverUser(ILogger<DeactivateUser> logger, UserManager<LinkUser> userManager, IAccountServiceMetrics metrics, ILinkUserModelFactory linkUserModelFactory, ICreateAuditEvent createAuditEvent)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _linkUserModelFactory = linkUserModelFactory ?? throw new ArgumentNullException(nameof(linkUserModelFactory));
            _createAuditEvent = createAuditEvent ?? throw new ArgumentNullException(nameof(createAuditEvent));
        }

        public async Task<LinkUserModel> Execute(ClaimsPrincipal? requestor, string userId, CancellationToken cancellationToken = default)
        {
            List<KeyValuePair<string, object?>> tagList = [new KeyValuePair<string, object?>(DiagnosticNames.UserId, userId)];
            using Activity? activity = ServiceActivitySource.Instance.StartActivityWithTags("RecoverUser:Execute", tagList);

            try
            {
                var user = await _userManager.FindByIdAsync(userId) ?? throw new ApplicationException($"User with id {userId} not found");

                if (!user.IsDeleted)
                {
                    throw new InvalidOperationException($"User with id {userId} has not been deleted");
                }

                user.IsDeleted = false;
                user.LastModifiedBy = requestor?.Claims.First(c => c.Type == "sub").Value;

                var result = await _userManager.UpdateAsync(user);

                if (!result.Succeeded)
                {
                    throw new ApplicationException($"Unable to recover user: {result.Errors}");
                }

                //generate tags for telemetry                
                foreach (var claim in user.Claims.Where(x => x.ClaimType == LinkAuthorizationConstants.LinkSystemClaims.Facility))
                {
                    var tag = new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, claim.ClaimValue);
                    tagList.Add(tag);
                    activity?.AddTag(tag.Key, tag.Value);
                }
                _metrics.IncrementAccountRestoredCounter(tagList);
                _logger.LogUserRecovery(userId, requestor?.Claims.First(c => c.Type == "sub").Value ?? "Unknown");

                //generate audit event
                var auditMessage = new AuditEventMessage
                {
                    FacilityId = user.Facilities?.Count > 0 ? user.Facilities.FirstOrDefault() : string.Empty,
                    Action = AuditEventType.Update,
                    EventDate = DateTime.UtcNow,
                    UserId = user.LastModifiedBy,
                    User = requestor?.Identity?.Name ?? string.Empty,
                    Resource = typeof(LinkUser).Name,
                    Notes = $"User ({user.Id}) recovered by '{user.LastModifiedBy}'."
                };

                _ = Task.Run(() => _createAuditEvent.Execute(auditMessage, cancellationToken));

                return _linkUserModelFactory.Create(user);

            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                _logger.LogDeleteUserException(userId, ex.Message);
                throw;
            }

        }
    }
}
