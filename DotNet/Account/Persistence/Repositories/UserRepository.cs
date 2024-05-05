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

        public async Task<List<LinkUser>> GetFacilityUsersAsync(string facilityId, CancellationToken cancellationToken = default)
        {
            var users = await _dbContext.Users.AsNoTracking()
                .Where(x => x.Facilities != null && x.Facilities.Contains(facilityId)).ToListAsync(cancellationToken);

            return users;
        }

        public async Task<List<LinkUser>> GetRoleUsersAsync(string role, CancellationToken cancellationToken = default)
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
    }
}
