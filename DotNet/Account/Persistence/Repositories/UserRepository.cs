using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Account.Persistence.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AccountDbContext _dbContext;

        public UserRepository(AccountDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<bool> CreateAsync(LinkUser entity, CancellationToken cancellationToken = default)
        {
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

        public async Task<bool> AddRoleAsync(string userId, LinkRole role, CancellationToken cancellationToken = default)
        {
            var user = await _dbContext.Users.FindAsync([userId], cancellationToken: cancellationToken);
            if (user is null)
            {
                return false;
            }

            user.UserRoles.Add(new LinkUserRole { UserId = userId, RoleId = role.Id });
            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;
        }

        public async Task<bool> AddRolesAsync(string userId, IEnumerable<LinkRole> roles, CancellationToken cancellationToken = default)
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

        public async Task<bool> RemoveRoleAsync(string userId, LinkRole role, CancellationToken cancellationToken = default)
        {
            var user = await _dbContext.Users.FindAsync([userId], cancellationToken: cancellationToken);
            if (user is null)
            {
                return false;
            }

            user.UserRoles.Remove(new LinkUserRole { UserId = userId, RoleId = role.Id });
            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;
        }

        public async Task<bool> RemoveRolesAsync(string userId, IEnumerable<LinkRole> roles, CancellationToken cancellationToken = default)
        {
            var user = await _dbContext.Users.FindAsync([userId], cancellationToken: cancellationToken);
            if (user is null)
            {
                return false;
            }

            foreach (var role in roles)
            {
                user.UserRoles.Remove(new LinkUserRole { UserId = userId, RoleId = role.Id });
            }

            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;
        }

        public async Task<IEnumerable<LinkUser>> GetFacilityUsersAsync(string facilityId, CancellationToken cancellationToken = default)
        {
            var users = await _dbContext.Users.AsNoTracking()
                .Where(x => x.Facilities != null && x.Facilities.Contains(facilityId)).ToListAsync(cancellationToken);

            return users;
        }

        public async Task<IEnumerable<LinkUser>> GetRoleUsersAsync(string role, CancellationToken cancellationToken = default)
        {
            var users = await _dbContext.Users.AsNoTracking()
                .Where(x => x.UserRoles.Any(r => r.Role.Name == role)).ToListAsync(cancellationToken);

            return users;
        }

        public async Task<LinkUser> GetUserAsync(string id, bool noTracking = true, CancellationToken cancellationToken = default)
        {
            var user = noTracking ?
                await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken) :
                await _dbContext.Users.FindAsync([id], cancellationToken);
            
            return user;
        }

        public async Task<LinkUser> GetUserByEmailAsync(string email, bool noTracking = true, CancellationToken cancellationToken = default)
        {
            var user = noTracking ?
                await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Email == email, cancellationToken) :
                await _dbContext.Users.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

            return user;
        }        

        public async Task<IEnumerable<LinkRole>> GetUserRoles(string userId, CancellationToken cancellationToken = default)
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
