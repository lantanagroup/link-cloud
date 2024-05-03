using LantanaGroup.Link.Account.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace LantanaGroup.Link.Account.Repositories
{
    public class AccountRepository : BaseRepository<LinkUser>
    {

        public AccountRepository(ILogger<AccountRepository> logger, DataContext dataContext) : base(logger, dataContext)
        {
        }


        public override async Task<LinkUser> GetAsync(Guid id, bool noTracking = false, CancellationToken cancellationToken = default)
        {
            var query = noTracking ? _dataContext.Accounts.AsNoTracking() : _dataContext.Accounts;
            return await query.Include(a => a.Groups).Include(a => a.Roles).FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<LinkUser> GetAccountByEmailAsync(string email, bool noTracking = false, CancellationToken cancellationToken = default)
        {
            var query = noTracking ? _dataContext.Accounts.AsNoTracking() : _dataContext.Accounts;
            return await query.Include(a => a.Groups).Include(a => a.Roles).FirstOrDefaultAsync(a => a.EmailAddress == email);
        }

        #region Account group management

        public async Task<LinkUser> AddAccountToGroup(Guid accountId, Guid groupId)
        {
            var account = await GetAsync(accountId, false);
            if (account is null) 
                return null;

            var group = await _dataContext.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
            if (group is null)
                return null;

            account.Groups.Add(group);
            await _dataContext.SaveChangesAsync();

            return await GetAsync(accountId);
        }

        public async Task RemoveAccountFromGroup(Guid accountId, Guid groupId)
        {
            var account = await _dataContext.Accounts.Where(a => a.Id == accountId).Include(a => a.Groups).FirstOrDefaultAsync();
            if (account is null)
                return;

            var group = await _dataContext.Groups.Where(g => g.Id == groupId).FirstOrDefaultAsync();
            if (group is null)
                return;

            account.Groups.Remove(group);
            await _dataContext.SaveChangesAsync();

        }

        #endregion


        #region Account role management

        public async Task<LinkUser> AddRoleToAccount(Guid accountId, Guid roleId)
        {
            var account = await GetAsync(accountId, false);
            if (account is null)
                return null;

            var role = await _dataContext.Roles.Where(g => g.Id == roleId).FirstOrDefaultAsync();
            if (role is null)
                return null;

            account.Roles.Add(role);
            await _dataContext.SaveChangesAsync();

            return await GetAsync(accountId);
        }

        public async Task RemoveRoleFromAccount(Guid accountId, Guid roleId)
        {
            var account = await _dataContext.Accounts.Where(a => a.Id == accountId).Include(a => a.Roles).FirstOrDefaultAsync();
            if (account is null)
                return;

            var role = await _dataContext.Roles.Where(g => g.Id == roleId).FirstOrDefaultAsync();
            if (role is null)
                return;

            account.Roles.Remove(role);
            await _dataContext.SaveChangesAsync();
        }

        #endregion

    }
}
