using LantanaGroup.Link.Account.Application.Interfaces.Factories.User;
using LantanaGroup.Link.Account.Application.Queries.User;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Enums;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.User.Handlers
{
    public static class SearchForFacilityUsers
    {
        public static async Task<IResult> Handle(
            HttpContext context, string id, [FromServices] ILogger<UserEndpoints> logger, [FromServices] ISearchFacilityUsers query, 
            [FromServices] IUserSearchFilterRecordFactory filterFactory,
            string? searchText,
            string? filterRoleBy,
            string? filterClaimBy,
            string? sortBy,
            SortOrder? sortOrder,
            int pageSize = 10,
            int pageNumber = 1)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return Results.BadRequest("A facility id is required");
                }

                // Create search filters
                var filters = filterFactory.Create(
                    searchText,
                    null,
                    filterRoleBy,
                    filterClaimBy,
                    false,
                    false,
                    sortBy,
                    sortOrder,
                    pageSize,
                    pageNumber);

                var users = await query.Execute(id, filters, context.RequestAborted);

                logger.LogSearchUsers(context.User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "Uknown", filters);

                return Results.Ok(users);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);

                // Create search filters
                var filters = filterFactory.Create(
                    searchText,
                    null,
                    filterRoleBy,
                    filterClaimBy,
                    false,
                    false,
                    sortBy,
                    sortOrder,
                    pageSize,
                    pageNumber);

                logger.LogSearchUsersException(ex.Message, filters);
                throw;
            }            
        }
    }
}
