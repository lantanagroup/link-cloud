using AutoMapper;
using Grpc.Core;
using LantanaGroup.Link.Account.Converters;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Protos;
using LantanaGroup.Link.Account.Repositories;
using LantanaGroup.Link.Shared.Application.Converters;
using LantanaGroup.Link.Shared.Application.Services;
using System.Text;

namespace LantanaGroup.Link.Account.Services
{

    public class GroupService : Protos.GroupService.GroupServiceBase
    {
        private readonly ILogger<GroupService> _logger;
        private readonly GroupRepository _groupRepository;

        private readonly IMapper _mapperModelToMessage;
        private readonly IMapper _mapperMessageToModel;
        private readonly ITenantApiService _tenantApiService;
        public GroupService(ILogger<GroupService> logger, GroupRepository GroupRepository, ITenantApiService tenantApiService)
        {
            _logger = logger;
            _groupRepository = GroupRepository;
            _tenantApiService = tenantApiService;

            _mapperModelToMessage = new ProtoMessageMapper<GroupModel, GroupMessage>(new AccountProfile()).CreateMapper();
            _mapperMessageToModel = new ProtoMessageMapper<GroupMessage, GroupModel>(new AccountProfile()).CreateMapper();
        }

        
        public override async Task GetAllGroups(GetAllGroupsMessage request, IServerStreamWriter<GroupMessage> responseStream, ServerCallContext context)
        {
            var res = await _groupRepository.GetAllAsync();

            foreach (var Group in res)
            {
                var message = _mapperModelToMessage.Map<GroupModel, GroupMessage>(Group);
                await responseStream.WriteAsync(message);
            }

        }

        public override async Task<GroupMessage> GetGroup(GetGroupMessage request, ServerCallContext context)
        {
            var res = await _groupRepository.GetAsync(Guid.Parse(request.Id));
            if (res == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, $"No Group found for {request.Id}"));
            }

            var acc = _mapperModelToMessage.Map<GroupModel, GroupMessage>(res);

            return acc;
        }


        public override async Task<GroupMessage> CreateGroup(GroupMessage request, ServerCallContext context)
        {
            var sb = new StringBuilder();
            foreach (var id in request.FacilityIds)
            {
                if (!(await _tenantApiService.CheckFacilityExists(id)))
                {
                    sb.AppendLine($"Facility {id} does not exist");
                }
            }

            if (sb.Length > 0)
            {
                throw new RpcException(new Status(StatusCode.NotFound, sb.ToString()));
            }

            var newGroup = _mapperMessageToModel.Map<GroupMessage, GroupModel>(request);

            try
            {
                await _groupRepository.AddAsync(newGroup);
            }
            catch (Exception ex)
            {
                _logger.LogError($"CreateGroup exception: {ex.Message}");
                throw;
            }

            return _mapperModelToMessage.Map<GroupModel, GroupMessage>(newGroup);
        }

        public override async Task<GroupMessage> UpdateGroup(GroupMessage request, ServerCallContext context)
        {
            var sb = new StringBuilder();
            foreach (var id in request.FacilityIds)
            {
                if (!(await _tenantApiService.CheckFacilityExists(id)))
                {
                    sb.AppendLine($"Facility {id} does not exist");
                }
            }

            if (sb.Length > 0)
            {
                throw new RpcException(new Status(StatusCode.NotFound, sb.ToString()));
            }

            var updatedGroup = _mapperMessageToModel.Map<GroupMessage, GroupModel>(request);

            if (updatedGroup.Id == Guid.Empty)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ID provided"));
            }

            GroupModel returnedModel;
            try
            {
                returnedModel = await _groupRepository.UpdateAsync(updatedGroup);
                if (returnedModel == null)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, $"No Group found for {updatedGroup.Id}"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"UpdateGroup exception: {ex.Message}");
                throw new RpcException(new Status(StatusCode.Internal, $"UpdateGroup exception: {ex.Message}"));
            }


            return _mapperModelToMessage.Map<GroupModel, GroupMessage>(returnedModel);
        }

        public override async Task<GroupDeletedMessage> DeleteGroup(DeleteGroupMessage request, ServerCallContext context)
        {
            
            try
            {
                await _groupRepository.DeleteAsync(Guid.Parse(request.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError($"DeleteGroup exception: {ex.Message}");
                throw new RpcException(new Status(StatusCode.Internal, $"DeleteGroup exception: {ex.Message}"));
            }

            return new GroupDeletedMessage();
            
        }

        public override async Task<GroupMessage> RestoreGroup(RestoreGroupMessage request, ServerCallContext context)
        {

            GroupModel restoredGroup;
            try
            {
                restoredGroup = await _groupRepository.RestoreAsync(Guid.Parse(request.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError($"RestoreGroup exception: {ex.Message}");
                throw new RpcException(new Status(StatusCode.Internal, $"RestoreGroup exception: {ex.Message}"));
            }

            return _mapperModelToMessage.Map<GroupModel, GroupMessage>(restoredGroup);
        }



        #region Group role management

        public override async Task<GroupMessage> AddRoleToGroup(AddRoleToGroupMessage request, ServerCallContext context)
        {
            GroupModel group;
            try
            {
                group = await _groupRepository.AddRoleToGroup(Guid.Parse(request.GroupId), Guid.Parse(request.RoleId));
            }
            catch (Exception ex)
            {
                _logger.LogError($"AddRoleToGroup exception: {ex.Message}");
                throw new RpcException(new Status(StatusCode.Internal, $"AddRoleToGroup exception: {ex.Message}"));
            }

            return _mapperModelToMessage.Map<GroupModel, GroupMessage>(group);
        }

        public override async Task<RoleRemovedFromGroupMessage> RemoveRoleFromGroup(RemoveRoleFromGroupMessage request, ServerCallContext context)
        {
            try
            {
                await _groupRepository.RemoveRoleFromGroup(Guid.Parse(request.GroupId), Guid.Parse(request.RoleId));
            }
            catch (Exception ex)
            {
                _logger.LogError($"RemoveRoleFromGroup exception: {ex.Message}");
                throw new RpcException(new Status(StatusCode.Internal, $"RemoveRoleFromGroup exception: {ex.Message}"));
            }

            return new RoleRemovedFromGroupMessage();
        }

        #endregion



    }
}