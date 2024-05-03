using AutoMapper;
using Google.Protobuf;
using Google.Protobuf.Collections;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Domain.Interfaces;
using LantanaGroup.Link.Account.Protos;
using LantanaGroup.Link.Shared.Application.Converters;

namespace LantanaGroup.Link.Account.Converters
{
    public class AccountProfile : Profile
    {
        public AccountProfile()
        {
            CreateMap<LinkUser, AccountMessage>();
            CreateMap<AccountMessage, LinkUser>();

            CreateMap<GroupModel, GroupMessage>();
            CreateMap<GroupMessage, GroupModel>();

            CreateMap<LinkRole, RoleMessage>();
            CreateMap<RoleMessage, LinkRole>();


            // Mappings to remove nested related entity collections as the JSON serializer for gRPC implementation doesn't currently seem to have an "ignore cycles" capability
            CreateMap<ICollection<LinkUser>, RepeatedField<AccountMessage>>().ConvertUsing(new AccountModelTypeConverter());
            CreateMap<ICollection<GroupModel>, RepeatedField<GroupMessage>>().ConvertUsing(new GroupModelTypeConverter());
            CreateMap<ICollection<LinkRole>, RepeatedField<RoleMessage>>().ConvertUsing(new RoleModelTypeConverter());
        }




        internal class AccountModelTypeConverter : ITypeConverter<ICollection<LinkUser>, RepeatedField<AccountMessage>>
        {
            IMapper _mappper = new ProtoMessageMapper<LinkUser, AccountMessage>().CreateMapper();

            public RepeatedField<AccountMessage> Convert(ICollection<LinkUser> source, RepeatedField<AccountMessage> destination, ResolutionContext context)
            {

                foreach (var account in source)
                {
                    if (account.Groups is not null)
                        account.Groups.Clear();
                    if (account.Roles is not null)
                        account.Roles.Clear();

                    destination.Add(_mappper.Map<LinkUser, AccountMessage>(account));
                }

                return destination;
            }
        }

        internal class GroupModelTypeConverter : ITypeConverter<ICollection<GroupModel>, RepeatedField<GroupMessage>>
        {
            IMapper _mappper = new ProtoMessageMapper<GroupModel, GroupMessage>().CreateMapper();

            public RepeatedField<GroupMessage> Convert(ICollection<GroupModel> source, RepeatedField<GroupMessage> destination, ResolutionContext context)
            {

                foreach (var group in source)
                {
                    if (group.Accounts is not null)
                        group.Accounts.Clear();
                    if (group.Roles is not null)
                        group.Roles.Clear();

                    destination.Add(_mappper.Map<GroupModel, GroupMessage>(group));
                }

                return destination;
            }
        }


        internal class RoleModelTypeConverter : ITypeConverter<ICollection<LinkRole>, RepeatedField<RoleMessage>>
        {
            IMapper _mappper = new ProtoMessageMapper<LinkRole, RoleMessage>().CreateMapper();

            public RepeatedField<RoleMessage> Convert(ICollection<LinkRole> source, RepeatedField<RoleMessage> destination, ResolutionContext context)
            {

                foreach (var role in source)
                {
                    if (role.Accounts is not null)
                        role.Accounts.Clear();
                    if (role.Groups is not null)
                        role.Groups.Clear();

                    destination.Add(_mappper.Map<LinkRole, RoleMessage>(role));
                }

                return destination;
            }
        }

    }

    

    public class CollectionTypeConverter : ITypeConverter<ICollection<IBaseEntity>, Google.Protobuf.Collections.RepeatedField<IMessage>>
    {
        public RepeatedField<IMessage> Convert(ICollection<IBaseEntity> source, RepeatedField<IMessage> destination, ResolutionContext context)
        {
            throw new NotImplementedException();
        }
    }
}
