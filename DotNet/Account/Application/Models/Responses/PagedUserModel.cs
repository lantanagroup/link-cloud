using LantanaGroup.Link.Account.Application.Models.User;
using LantanaGroup.Link.Shared.Application.Interfaces.Models;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.Account.Application.Models.Responses
{
    public class PagedUserModel : IPagedModel<LinkUserModel>
    {
        public List<LinkUserModel> Records { get; set; } = [];
        public PaginationMetadata Metadata { get; set; } = null!;

        public PagedUserModel(): this([], new PaginationMetadata()) { }

        public PagedUserModel(List<LinkUserModel> records, PaginationMetadata metadata)
        {
            Records = records;
            Metadata = metadata;
        }
    }
}
