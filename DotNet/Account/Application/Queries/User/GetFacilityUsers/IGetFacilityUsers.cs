using LantanaGroup.Link.Account.Application.Models.Responses;

namespace LantanaGroup.Link.Account.Application.Queries.User.GetFacilityUsers
{
    public interface IGetFacilityUsers
    {
        Task<List<GroupedUserModel>> Execute(string facilityId, CancellationToken cancellationToken = default);
    }
}
