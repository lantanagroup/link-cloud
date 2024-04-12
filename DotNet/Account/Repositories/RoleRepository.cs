using LantanaGroup.Link.Account.Domain.Entities;

namespace LantanaGroup.Link.Account.Repositories
{
    public class RoleRepository : BaseRepository<RoleModel>
    {

        public RoleRepository(ILogger<RoleRepository> logger, DataContext dataContext) : base(logger, dataContext)
        {
        }


    }
}
