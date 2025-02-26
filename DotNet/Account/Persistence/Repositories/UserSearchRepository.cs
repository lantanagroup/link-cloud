using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace LantanaGroup.Link.Account.Persistence.Repositories
{
    public class UserSearchRepository : IUserSearchRepository
    {
        private readonly AccountDbContext _dbContext;

        public UserSearchRepository(AccountDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<(IEnumerable<LinkUser>, PaginationMetadata)> SearchAsync(string? searchText, string? filterFacilityBy, string? filterRoleBy, string? filterClaimBy, bool includeDeactivatedUsers, bool includeDeletedUsers, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default)
        {
            IEnumerable<LinkUser> users;
            var query = _dbContext.Users.AsNoTracking().AsQueryable();
            query = query.Include(x => x.UserRoles).ThenInclude(x => x.Role);

            #region Build Query
            if (searchText is not null && searchText.Length > 0)
            {
                //build free text search if search text is provided
                query = query.Where(x =>
                    (x.FirstName != null && x.FirstName.Contains(searchText)) ||
                    (x.MiddleName != null && x.MiddleName.Contains(searchText)) ||
                    (x.LastName != null && x.LastName.Contains(searchText)) ||
                    (x.Email != null && x.Email.Contains(searchText)) ||
                    (x.UserName != null && x.UserName.Contains(searchText))
                );
            }

            //filter by facility
            //if (filterFacilityBy is not null && filterFacilityBy.Length > 0)
            //{
            //    query = query.Where(x => x.Facilities != null && x.Facilities.Contains(filterFacilityBy));
            //}

            //filter by role
            if (filterRoleBy is not null && filterRoleBy.Length > 0)
            {
                query = query.Where(x => x.UserRoles.Any(r => r.Role.Name == filterRoleBy));
            }

            //filter by claim
            if (filterClaimBy is not null && filterClaimBy.Length > 0)
            {
                query = query.Where(x => x.Claims.Any(c => c.ClaimValue == filterClaimBy));
            }

            //filter by active status
            if (!includeDeactivatedUsers)
            {
                query = query.Where(x => x.IsActive);
            }

            //filter by deleted status
            if (!includeDeletedUsers)
            {
                query = query.Where(x => !x.IsDeleted);
            }              
            #endregion

            query = sortOrder switch
            {
                SortOrder.Ascending => query.OrderBy(SetSortBy<LinkUser>(sortBy)),
                SortOrder.Descending => query.OrderByDescending(SetSortBy<LinkUser>(sortBy)),
                _ => query.OrderBy(x => x.CreatedOn)
            };

            //get total count
            var totalRecords = await query.CountAsync(cancellationToken);

            //get users
            users = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var metadata = new PaginationMetadata(pageSize, pageNumber, totalRecords);

            return (users, metadata);

        }

        public async Task<(IEnumerable<LinkUser>, PaginationMetadata)> FacilitySearchAsync(string facilityId, string? searchText, string? filterRoleBy, string? filterClaimBy, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default)
        {
            IEnumerable<LinkUser> users;
            var query = _dbContext.Users.AsNoTracking().AsQueryable();

            //filter by facility
            //query = query.Where(x => x.Facilities != null && x.Facilities.Contains(facilityId));
            query = query.Where(x => x.IsActive);
            query = query.Where(x => !x.IsDeleted);

            #region Build Query
            if (searchText is not null && searchText.Length > 0)
            {
                //build free text search if search text is provided
                query = query.Where(x =>
                    (x.FirstName != null && x.FirstName.Contains(searchText)) ||
                    (x.MiddleName != null && x.MiddleName.Contains(searchText)) ||
                    (x.LastName != null && x.LastName.Contains(searchText)) ||
                    (x.Email != null && x.Email.Contains(searchText)) ||
                    (x.UserName != null && x.UserName.Contains(searchText))
                );
            }            

            //filter by role
            if (filterRoleBy is not null && filterRoleBy.Length > 0)
            {
                query = query.Where(x => x.UserRoles.Any(r => r.Role.Name == filterRoleBy));
            }

            //filter by claim
            if (filterClaimBy is not null && filterClaimBy.Length > 0)
            {
                query = query.Where(x => x.Claims.Any(c => c.ClaimValue == filterClaimBy));
            }

            #endregion

            query = sortOrder switch
            {
                SortOrder.Ascending => query.OrderBy(SetSortBy<LinkUser>(sortBy)),
                SortOrder.Descending => query.OrderByDescending(SetSortBy<LinkUser>(sortBy)),
                _ => query.OrderBy(x => x.CreatedOn)
            };

            //get total count
            var totalRecords = await query.CountAsync(cancellationToken);

            //get users
            users = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var metadata = new PaginationMetadata(pageSize, pageNumber, totalRecords);

            return (users, metadata);

        }

        /// <summary>
        /// Creates a sort expression for the given sortBy parameter
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sortBy"></param>
        /// <returns></returns>
        private static Expression<Func<T, object>> SetSortBy<T>(string? sortBy)
        {
            var sortKey = sortBy switch
            {
                "FirstName" => "FirstName",
                "LastName" => "LastName",
                "UserName" => "UserName",
                "Email" => "Email",
                "CreatedOn" => "CreatedOn",
                "LastModifiedOn" => "LastModifiedOn",
                _ => "CreatedOn"
            };

            var parameter = Expression.Parameter(typeof(T), "p");
            var sortExpression = Expression.Lambda<Func<T, object>>(Expression.Convert(Expression.Property(parameter, sortKey), typeof(object)), parameter);

            return sortExpression;
        }
    }
}
