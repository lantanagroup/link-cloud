using LantanaGroup.Link.Account.Application.Interfaces.Factories.User;
using LantanaGroup.Link.Account.Application.Queries.User;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Enums;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Text.Json;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.User.Handlers
{
    public static class SearchForUsers
    {
        public static async Task<IResult> Handle(
            HttpContext context, [FromServices] ILogger<UserEndpoints> logger, [FromServices] ISearchUsers query, 
            [FromServices] IUserSearchFilterRecordFactory filterFactory,
            string? searchText,
            string? filterFacilityBy,
            string? filterRoleBy,
            string? filterClaimBy,
            bool? includeDeactivatedUsers,
            bool? includeDeletedUsers,
            string? sortBy,
            SortOrder? sortOrder,
            int pageSize = 10,
            int pageNumber = 1)
        {
            try
            {
                // Create search filters
                var filters = filterFactory.Create(
                    searchText,
                    filterFacilityBy,
                    filterRoleBy,
                    filterClaimBy,
                    includeDeactivatedUsers ?? false,
                    includeDeletedUsers ?? false,
                    sortBy,
                    sortOrder,
                    pageSize,
                    pageNumber);

                var users = await query.Execute(filters, context.RequestAborted);

                logger.LogSearchUsers(context.User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "Uknown", filters);

                //add X-Pagination header for machine-readable pagination metadata
                context.Response.Headers["X-Pagination"] = JsonSerializer.Serialize(users.Metadata);

                return Results.Ok(users);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);

                // Create search filters
                var filters = filterFactory.Create(
                    searchText,
                    filterFacilityBy,
                    filterRoleBy,
                    filterClaimBy,
                    includeDeactivatedUsers ?? false,
                    includeDeletedUsers ?? false,
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
