﻿using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.Role.DeleteRole
{
    public interface IDeleteRole
    {
        Task<bool> Execute(ClaimsPrincipal? requestor, string roleId, CancellationToken cancellationToken = default);
    }
}
