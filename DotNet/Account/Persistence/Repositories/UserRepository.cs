using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Application.Models;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Persistence.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly ILogger<UserRepository> _logger;
        private readonly AccountDbContext _dbContext;
        private readonly IOptions<UserManagementSettings> _userManagementOptions;

        public UserRepository(ILogger<UserRepository> logger, AccountDbContext dbContext, IOptions<UserManagementSettings> userManagementOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));            
            _userManagementOptions = userManagementOptions ?? throw new ArgumentNullException(nameof(userManagementOptions));
        }

        public async Task<bool> CreateAsync(LinkUser entity, CancellationToken cancellationToken = default)
        {
            entity.IsActive = _userManagementOptions.Value.EnableAutomaticUserActivation;          

            await _dbContext.Users.AddAsync(entity, cancellationToken);
            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;
        }

        public async Task<bool> UpdateAsync(LinkUser entity, CancellationToken cancellationToken = default)
        {
            var currentUser = await _dbContext.Users.FindAsync([entity.Id], cancellationToken: cancellationToken);
            if (currentUser is null)
            {
                return false;
            }

            _dbContext.Entry(currentUser).CurrentValues.SetValues(entity);
            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;
        }

        public async Task<bool> AddRoleAsync(Guid userId, LinkRole role, CancellationToken cancellationToken = default)
        {
            var user = await _dbContext.Users.FindAsync([userId], cancellationToken: cancellationToken);
            if (user is null)
            {
                return false;
            }

            user.UserRoles.Add(new LinkUserRole { UserId = userId, RoleId = role.Id });
            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;
        }

        public async Task<bool> AddRolesAsync(Guid userId, IEnumerable<LinkRole> roles, CancellationToken cancellationToken = default)
        {
            var user = await _dbContext.Users.FindAsync([userId], cancellationToken: cancellationToken);

            if (user is null)
            {
                return false;
            }

            foreach(var role in roles)
            {
                user.UserRoles.Add(new LinkUserRole { UserId = userId, RoleId = role.Id });
            }
            
            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;
        }

        public async Task<bool> RemoveRoleAsync(Guid userId, LinkRole role, CancellationToken cancellationToken = default)
        {
            var user = await _dbContext.Users
                .Include(x => x.UserRoles)
                .Where(x => x.Id == userId).FirstOrDefaultAsync(cancellationToken);

            if (user is null)
            {
                return false;
            }

            var roleToRemove = user.UserRoles.FirstOrDefault(x => x.UserId == userId && x.RoleId == role.Id);
            if (roleToRemove is not null)
            {
                user.UserRoles.Remove(roleToRemove);
            }
            else
            {
                return false;
            }

            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;
        }

        public async Task<bool> RemoveRolesAsync(Guid userId, IEnumerable<LinkRole> roles, CancellationToken cancellationToken = default)
        {
            var user = await _dbContext.Users
                .Include(x => x.UserRoles)
                .Where(x => x.Id == userId).FirstOrDefaultAsync(cancellationToken);

            if (user is null)
            {
                return false;
            }

            foreach (var role in roles)
            {
                
                var roleToRemove = user.UserRoles.FirstOrDefault(x => x.UserId == userId && x.RoleId == role.Id);
                if (roleToRemove is not null)
                {
                    user.UserRoles.Remove(roleToRemove);
                }
                else
                {
                    return false;
                }
            }

            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;
        }

        public async Task<bool> AddClaimToUserAsync(Guid userId, Claim claim, CancellationToken cancellationToken = default)
        {
            var user = await _dbContext.Users.FindAsync([userId], cancellationToken: cancellationToken);

            if (user is null)
            {
                return false;
            }

            user.Claims.Add(new LinkUserClaim { UserId = userId, ClaimType = claim.Type, ClaimValue = claim.Value });

            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;
        }

        public async Task<bool> RemoveClaimFromUserAsync(Guid userId, Claim claim, CancellationToken cancellationToken = default)
        {
            var user = await _dbContext.Users
                .Include(x => x.Claims)
                .Where(x => x.Id == userId).FirstOrDefaultAsync(cancellationToken);
            if (user is null)
            {
                return false;
            }

            var claimToRemove = user.Claims.FirstOrDefault(x => x.UserId == userId && x.ClaimType == claim.Type && x.ClaimValue == claim.Value);
            if (claimToRemove is not null)
            {
                user.Claims.Remove(claimToRemove);
            }
            else
            {
                return false;
            }

            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;
        }

        public async Task<bool> AddClaimsToUserAsync(Guid userId, IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
        {
            var user = await _dbContext.Users.FindAsync([userId], cancellationToken: cancellationToken);

            if (user is null)
            {
                return false;
            }

            int addedClaims = 0;
            foreach (var claim in claims)
            {
                user.Claims.Add(new LinkUserClaim { UserId = userId, ClaimType = claim.Type, ClaimValue = claim.Value });
                
                if(await _dbContext.SaveChangesAsync(cancellationToken) > 0)
                {
                    _logger.LogUserClaimAssignment(userId.ToString(), claim.Type, claim.Value, string.Empty);
                    addedClaims++;
                }
                else
                {
                    _logger.LogUserClaimAssignmentException(userId.ToString(), claim.Type, claim.Value, "Failed to add claim to user");
                }
            }

            return addedClaims > 0;
        }

        public async Task<bool> RemoveClaimsFromUserAsync(Guid userId, IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
        {
            var user = await _dbContext.Users
                .Include(x => x.Claims)
                .Where(x => x.Id == userId).FirstOrDefaultAsync(cancellationToken);

            if (user is null)
            {
                return false;
            }

            int removedClaims = 0;
            foreach (var claim in claims)
            {
                var claimToRemove = user.Claims.FirstOrDefault(x => x.UserId == userId && x.ClaimType == claim.Type && x.ClaimValue == claim.Value);
                if (claimToRemove is not null)
                {
                    user.Claims.Remove(claimToRemove);
                }
                else
                {
                    _logger.LogUserClaimRemovalException(userId.ToString(), claim.Type, claim.Value, "Failed to remove claim from user");
                }                

                if (await _dbContext.SaveChangesAsync(cancellationToken) > 0)
                {
                    _logger.LogUserClaimRemoval(userId.ToString(), claim.Type, claim.Value, string.Empty);
                    removedClaims++;
                }
                else
                {
                    _logger.LogUserClaimRemovalException(userId.ToString(), claim.Type, claim.Value, "Failed to remove claim from user");
                }
            }

            return removedClaims > 0;
        }

        //public async Task<IEnumerable<LinkUser>> GetFacilityUsersAsync(string facilityId, CancellationToken cancellationToken = default)
        //{
        //    var users = await _dbContext.Users.AsNoTracking()
        //        .Where(x => x.Facilities != null && x.Facilities.Contains(facilityId)).ToListAsync(cancellationToken);

        //    return users;
        //}

        public async Task<IEnumerable<LinkUser>> GetRoleUsersAsync(string role, CancellationToken cancellationToken = default)
        {
            var users = await _dbContext.Users.AsNoTracking()
                .Where(x => x.UserRoles.Any(r => r.Role.Name == role)).ToListAsync(cancellationToken);

            return users;
        }

        public async Task<LinkUser> GetUserAsync(Guid id, bool noTracking = true, CancellationToken cancellationToken = default)
        {
            var user = noTracking ?
                await _dbContext.Users.AsNoTracking()
                    .Include(x => x.UserRoles)
                        .ThenInclude(x => x.Role)
                            .ThenInclude(x => x.RoleClaims)
                    .Include(x => x.Claims)
                    .FirstOrDefaultAsync(x => x.Id == id, cancellationToken) :
                await _dbContext.Users
                    .Include(x => x.UserRoles)
                        .ThenInclude(x => x.Role)
                            .ThenInclude(x => x.RoleClaims)
                    .Include(x => x.Claims)
                    .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            return user;
        }

        public async Task<LinkUser> GetUserByEmailAsync(string email, bool noTracking = true, CancellationToken cancellationToken = default)
        {
            var user = noTracking ?
                await _dbContext.Users.AsNoTracking()
                    .Include(x => x.UserRoles)
                        .ThenInclude(x => x.Role)
                            .ThenInclude(x => x.RoleClaims)
                    .Include(x => x.Claims)
                    .FirstOrDefaultAsync(x => x.Email == email, cancellationToken) :
                await _dbContext.Users
                    .Include(x => x.UserRoles)
                        .ThenInclude(x => x.Role)
                            .ThenInclude(x => x.RoleClaims)
                    .Include(x => x.Claims)
                    .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

            return user;
        }        

        public async Task<IEnumerable<LinkRole>> GetUserRoles(Guid userId, CancellationToken cancellationToken = default)
        {
            var roles = await _dbContext.Users.AsNoTracking()
                .Where(x => x.Id == userId)
                .SelectMany(x => x.UserRoles)
                .Select(x => x.Role)
                .ToListAsync(cancellationToken);

            return roles;
        }        
    }
}
