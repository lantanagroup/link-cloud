using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Account.Persistence.Repositories
{
    public class RoleRepository : IRoleRepository
    {
        private readonly AccountDbContext _dbContext;

        public RoleRepository(AccountDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<LinkRole> GetRoleAsync(string roleId, bool noTracking = true, CancellationToken cancellationToken = default)
        {
            var role = noTracking ?
                await _dbContext.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == roleId, cancellationToken) :
                await _dbContext.Roles.FirstOrDefaultAsync(x => x.Id == roleId, cancellationToken);

            return role;
        }

        public async Task<LinkRole> GetRoleByNameAsync(string roleName, bool noTracking = true, CancellationToken cancellationToken = default)
        {
            var role = noTracking ?
                await _dbContext.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Name == roleName, cancellationToken) :
                await _dbContext.Roles.FirstOrDefaultAsync(x => x.Name == roleName, cancellationToken);

            return role;
        }

        public async Task<IEnumerable<LinkRole>> GetRolesAsync(CancellationToken cancellationToken = default)
        {
            var roles = await _dbContext.Roles.AsNoTracking().ToListAsync(cancellationToken);                

            return roles;
        }
    }
}
