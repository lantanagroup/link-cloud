using AutoMapper;
using Grpc.Core;
using LantanaGroup.Link.Account.Converters;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Protos;
using LantanaGroup.Link.Account.Repositories;
using LantanaGroup.Link.Shared.Application.Converters;

namespace LantanaGroup.Link.Account.Services
{

    public class RoleService : Protos.RoleService.RoleServiceBase
    {
        private readonly ILogger<RoleService> _logger;
        private readonly RoleRepository _RoleRepository;

        private readonly IMapper _mapperModelToMessage;
        private readonly IMapper _mapperMessageToModel;

        public RoleService(ILogger<RoleService> logger, RoleRepository RoleRepository)
        {
            _logger = logger;
            _RoleRepository = RoleRepository;

            _mapperModelToMessage = new ProtoMessageMapper<RoleModel, RoleMessage>(new AccountProfile()).CreateMapper();
            _mapperMessageToModel = new ProtoMessageMapper<RoleMessage, RoleModel>(new AccountProfile()).CreateMapper();
        }

        
        public override async Task GetAllRoles(GetAllRolesMessage request, IServerStreamWriter<RoleMessage> responseStream, ServerCallContext context)
        {
            var res = await _RoleRepository.GetAllAsync();

            foreach (var Role in res)
            {
                var message = _mapperModelToMessage.Map<RoleModel, RoleMessage>(Role);
                await responseStream.WriteAsync(message);
            }

        }

        public override async Task<RoleMessage> GetRole(GetRoleMessage request, ServerCallContext context)
        {
            var res = await _RoleRepository.GetAsync(Guid.Parse(request.Id));
            if (res == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, $"No Role found for {request.Id}"));
            }

            var acc = _mapperModelToMessage.Map<RoleModel, RoleMessage>(res);

            return acc;
        }


        public override async Task<RoleMessage> CreateRole(RoleMessage request, ServerCallContext context)
        {
            var newRole = _mapperMessageToModel.Map<RoleMessage, RoleModel>(request);

            try
            {
                await _RoleRepository.AddAsync(newRole);
            }
            catch (Exception ex)
            {
                _logger.LogError($"CreateRole exception: {ex.Message}");
                throw;
            }

            return _mapperModelToMessage.Map<RoleModel, RoleMessage>(newRole);
        }

        public override async Task<RoleMessage> UpdateRole(RoleMessage request, ServerCallContext context)
        {
            var updatedRole = _mapperMessageToModel.Map<RoleMessage, RoleModel>(request);

            if (updatedRole.Id == Guid.Empty)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ID provided"));
            }

            RoleModel returnedModel;
            try
            {
                returnedModel = await _RoleRepository.UpdateAsync(updatedRole);
                if (returnedModel == null)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, $"No Role found for {updatedRole.Id}"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"UpdateRole exception: {ex.Message}");
                throw new RpcException(new Status(StatusCode.Internal, $"UpdateRole exception: {ex.Message}"));
            }


            return _mapperModelToMessage.Map<RoleModel, RoleMessage>(returnedModel);
        }

        public override async Task<RoleDeletedMessage> DeleteRole(DeleteRoleMessage request, ServerCallContext context)
        {
            
            try
            {
                await _RoleRepository.DeleteAsync(Guid.Parse(request.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError($"DeleteRole exception: {ex.Message}");
                throw new RpcException(new Status(StatusCode.Internal, $"DeleteRole exception: {ex.Message}"));
            }

            return new RoleDeletedMessage();
            
        }

        public override async Task<RoleMessage> RestoreRole(RestoreRoleMessage request, ServerCallContext context)
        {

            RoleModel restoredRole;
            try
            {
                restoredRole = await _RoleRepository.RestoreAsync(Guid.Parse(request.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError($"RestoreRole exception: {ex.Message}");
                throw new RpcException(new Status(StatusCode.Internal, $"RestoreRole exception: {ex.Message}"));
            }

            return _mapperModelToMessage.Map<RoleModel, RoleMessage>(restoredRole);
        }


    }
}