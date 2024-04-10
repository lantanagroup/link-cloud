using LantanaGroup.Link.Account.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Account.Repositories
{
    public class GroupRepository : BaseRepository<GroupModel>
    {

        public GroupRepository(ILogger<GroupRepository> logger, DataContext dataContext) : base(logger, dataContext)
        {
        }



        public override async Task<GroupModel> GetAsync(Guid id, bool noTracking = false, CancellationToken cancellationToken = default)
        {
            var query = noTracking ? _dataContext.Groups.AsNoTracking() : _dataContext.Groups;
            return await query.Include(a => a.Roles).FirstOrDefaultAsync(a => a.Id == id);
        }

        #region Group role management

        public async Task<GroupModel> AddRoleToGroup(Guid groupId, Guid roleId)
        {
            var group = await GetAsync(groupId, false);
            if (group is null)
                return null;

            var role = await _dataContext.Roles.Where(r => r.Id == roleId).FirstOrDefaultAsync();
            if (role is null)
                return null;

            group.Roles.Add(role);
            await _dataContext.SaveChangesAsync();

            return await GetAsync(groupId);
        }

        public async Task RemoveRoleFromGroup(Guid groupId, Guid roleId)
        {
            var group = await _dataContext.Groups.Where(g => g.Id == groupId).Include(g => g.Roles).FirstOrDefaultAsync();
            if (group is null)
                return;

            var role = await _dataContext.Roles.Where(r => r.Id == roleId).FirstOrDefaultAsync();
            if (role is null)
                return;

            group.Roles.Remove(role);
            await _dataContext.SaveChangesAsync();
        }

        #endregion

    }
}
