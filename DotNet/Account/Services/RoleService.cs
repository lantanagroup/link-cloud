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

    public class RoleService : Protos.RoleService.RoleServiceBase
    {
        private readonly ILogger<RoleService> _logger;
        private readonly RoleRepository _roleRepository;

        private readonly IMapper _mapperModelToMessage;
        private readonly IMapper _mapperMessageToModel;
        private readonly ITenantApiService _tenantApiService;

        public RoleService(ILogger<RoleService> logger, RoleRepository roleRepository, ITenantApiService tenantApiService)
        {
            _logger = logger;
            _roleRepository = roleRepository;
            _tenantApiService = tenantApiService;

            _mapperModelToMessage = new ProtoMessageMapper<LinkRole, RoleMessage>(new AccountProfile()).CreateMapper();
            _mapperMessageToModel = new ProtoMessageMapper<RoleMessage, LinkRole>(new AccountProfile()).CreateMapper();
        }

        
        public override async Task GetAllRoles(GetAllRolesMessage request, IServerStreamWriter<RoleMessage> responseStream, ServerCallContext context)
        {
            var res = await _roleRepository.GetAllAsync();

            foreach (var Role in res)
            {
                var message = _mapperModelToMessage.Map<LinkRole, RoleMessage>(Role);
                await responseStream.WriteAsync(message);
            }

        }

        public override async Task<RoleMessage> GetRole(GetRoleMessage request, ServerCallContext context)
        {
            var res = await _roleRepository.GetAsync(Guid.Parse(request.Id));
            if (res == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, $"No Role found for {request.Id}"));
            }

            var acc = _mapperModelToMessage.Map<LinkRole, RoleMessage>(res);

            return acc;
        }


        public override async Task<RoleMessage> CreateRole(RoleMessage request, ServerCallContext context)
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

            var newRole = _mapperMessageToModel.Map<RoleMessage, LinkRole>(request);

            try
            {
                await _roleRepository.AddAsync(newRole);
            }
            catch (Exception ex)
            {
                _logger.LogError($"CreateRole exception: {ex.Message}");
                throw;
            }

            return _mapperModelToMessage.Map<LinkRole, RoleMessage>(newRole);
        }

        public override async Task<RoleMessage> UpdateRole(RoleMessage request, ServerCallContext context)
        {
            var sb = new StringBuilder();
            foreach (var id in request.FacilityIds)
            {
                if (!await _tenantApiService.CheckFacilityExists(id))
                {
                    sb.AppendLine($"Facility {id} does not exist");
                }
            }

            if (sb.Length > 0)
            {
                throw new RpcException(new Status(StatusCode.NotFound, sb.ToString()));
            }

            var updatedRole = _mapperMessageToModel.Map<RoleMessage, LinkRole>(request);

            if (updatedRole.Id == Guid.Empty)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ID provided"));
            }

            LinkRole returnedModel;
            try
            {
                returnedModel = await _roleRepository.UpdateAsync(updatedRole);
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


            return _mapperModelToMessage.Map<LinkRole, RoleMessage>(returnedModel);
        }

        public override async Task<RoleDeletedMessage> DeleteRole(DeleteRoleMessage request, ServerCallContext context)
        {
            
            try
            {
                await _roleRepository.DeleteAsync(Guid.Parse(request.Id));
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

            LinkRole restoredRole;
            try
            {
                restoredRole = await _roleRepository.RestoreAsync(Guid.Parse(request.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError($"RestoreRole exception: {ex.Message}");
                throw new RpcException(new Status(StatusCode.Internal, $"RestoreRole exception: {ex.Message}"));
            }

            return _mapperModelToMessage.Map<LinkRole, RoleMessage>(restoredRole);
        }


    }
}