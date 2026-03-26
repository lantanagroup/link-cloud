using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Persistence.Repositories
{
    public class RoleRepository : IRoleRepository
    {
        private readonly AccountDbContext _dbContext;

        public RoleRepository(AccountDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }        

        public async Task<bool> CreateAsync(LinkRole entity, CancellationToken cancellationToken = default)
        {
            await _dbContext.Roles.AddAsync(entity, cancellationToken);
            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;
        }

        public async Task<bool> UpdateAsync(LinkRole entity, CancellationToken cancellationToken = default)
        {
            var currentRole = await _dbContext.Roles.FindAsync([entity.Id], cancellationToken);
            if(currentRole is null)
            {
                return false;
            }

            _dbContext.Entry(currentRole).CurrentValues.SetValues(entity);
            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var role = await _dbContext.Roles.FindAsync([id], cancellationToken);
            if(role is null)
            {
                return false;
            }

            _dbContext.Roles.Remove(role);
            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;
        }

        public async Task<bool> AddClaimAsync(Guid roleId, Claim claim, CancellationToken cancellationToken = default)
        {
            var role = await _dbContext.Roles
                .Include(x => x.RoleClaims)
                .FirstOrDefaultAsync(x => x.Id == roleId, cancellationToken);

            if (role is null)
            {
                return false;
            }

            if(role.RoleClaims.Any(x => x.ClaimType == claim.Type && x.ClaimValue == claim.Value))
            {
               return true;
            }

            role.RoleClaims.Add(new LinkRoleClaim { RoleId = roleId, ClaimType = claim.Type, ClaimValue = claim.Value });

            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;
        }

        public async Task<bool> RemoveClaimAsync(Guid roleId, Claim claim, CancellationToken cancellationToken = default)
        {
            var role = await _dbContext.Roles
                .Include(x => x.RoleClaims)
                .FirstOrDefaultAsync(x => x.Id == roleId, cancellationToken);

            if (role is null)
            {
                return false;
            }

            var roleClaim = role.RoleClaims.FirstOrDefault(x => x.ClaimType == claim.Type && x.ClaimValue == claim.Value);
            if(roleClaim is null)
            {
                return true;
            }

            role.RoleClaims.Remove(roleClaim);
            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;            
        }

        public async Task<LinkRole> GetRoleAsync(Guid roleId, bool noTracking = true, CancellationToken cancellationToken = default)
        {
            var role = noTracking ?
                await _dbContext.Roles.AsNoTracking()
                    .Include(x => x.RoleClaims)
                    .FirstOrDefaultAsync(x => x.Id == roleId, cancellationToken) :
                await _dbContext.Roles
                    .Include(x => x.RoleClaims)
                    .FirstOrDefaultAsync(x => x.Id == roleId, cancellationToken);

            return role;
        }

        public async Task<LinkRole> GetRoleByNameAsync(string roleName, bool noTracking = true, CancellationToken cancellationToken = default)
        {
            var role = noTracking ?
                await _dbContext.Roles.AsNoTracking()
                    .Include(x => x.RoleClaims)
                    .FirstOrDefaultAsync(x => x.Name == roleName, cancellationToken) :
                await _dbContext.Roles
                    .Include(x => x.RoleClaims)
                    .FirstOrDefaultAsync(x => x.Name == roleName, cancellationToken);

            return role;
        }

        public async Task<IEnumerable<LinkRole>> GetRolesAsync(CancellationToken cancellationToken = default)
        {
            var roles = await _dbContext.Roles.AsNoTracking().ToListAsync(cancellationToken);                

            return roles;
        }

        public async Task<IEnumerable<Claim>> GetClaimsAsync(Guid roleId, CancellationToken cancellationToken = default)
        {
            var roleClaims = await _dbContext.Roles.AsNoTracking()
                .Where(x => x.Id == roleId)
                .SelectMany(x => x.RoleClaims)                
                .ToListAsync(cancellationToken);

            var claims = roleClaims.Select(x => x.ToClaim());

            return claims;
        }
    }
}
