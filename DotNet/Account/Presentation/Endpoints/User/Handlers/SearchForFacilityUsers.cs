using LantanaGroup.Link.Account.Application.Interfaces.Factories.User;
using LantanaGroup.Link.Account.Application.Queries.User;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Enums;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.User.Handlers
{
    public static class SearchForFacilityUsers
    {
        public static async Task<IResult> Handle(
            HttpContext context, string id, ILogger logger, ISearchFacilityUsers query, IUserSearchFilterRecordFactory filterFactory,
            string? searchText,
            string? filterRoleBy,
            string? filterClaimBy,
            string? sortBy,
            SortOrder? sortOrder,
            int pageSize = 10,
            int pageNumber = 1)
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

            logger.LogFindUsers(context.User.Claims.First(c => c.Type == "sub").Value ?? "Uknown");

            return Results.Ok(users);
        }
    }
}
