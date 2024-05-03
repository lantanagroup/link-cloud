using System.Text;
using AutoMapper;
using Grpc.Core;
using LantanaGroup.Link.Account.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Account.Converters;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Protos;
using LantanaGroup.Link.Account.Repositories;
using LantanaGroup.Link.Shared.Application.Converters;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Services;

namespace LantanaGroup.Link.Account.Services
{

    public class AccountService : Protos.AccountService.AccountServiceBase
    {
        private readonly ILogger<AccountService> _logger;
        private readonly AccountRepository _accountRepository;

        private readonly IMapper _mapperModelToMessage;
        private readonly IMapper _mapperMessageToModel;
        private readonly ITenantApiService _tenantApiService;
        private readonly IAccountServiceMetrics _metrics;

        public AccountService(ILogger<AccountService> logger, AccountRepository accountRepository, ITenantApiService tenantApiService, IAccountServiceMetrics metrics)
        {
            _logger = logger;
            _accountRepository = accountRepository;
            _tenantApiService = tenantApiService;

            _mapperModelToMessage = new ProtoMessageMapper<LinkUser, AccountMessage>(new AccountProfile()).CreateMapper();
            _mapperMessageToModel = new ProtoMessageMapper<AccountMessage, LinkUser>(new AccountProfile()).CreateMapper();
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        }

        /*
         *
         * AccountService:

            CreateAccount

            UpdateAccount (If the body of the request contains a facility Id)

            GroupService:

            CreateGroup

            UpdateGroup  (If the body of the request contains a facility Id)

            RoleService

            CreateRole

            UpdateRole  (If the body of the request contains a facility Id)
         */

        #region Basic CRUD

        public override async Task GetAllAccounts(GetAllAccountsMessage request, IServerStreamWriter<AccountMessage> responseStream, ServerCallContext context)
        {
            var res = await _accountRepository.GetAllAsync();

            foreach (var account in res)
            {
                var message = _mapperModelToMessage.Map<LinkUser, AccountMessage>(account);
                await responseStream.WriteAsync(message);
            }

        }

        public override async Task<AccountMessage> GetAccount(GetAccountMessage request, ServerCallContext context)
        {
            var res = await _accountRepository.GetAsync(Guid.Parse(request.Id));
            if (res == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, $"No account found for {request.Id}"));
            }

            return _mapperModelToMessage.Map<LinkUser, AccountMessage>(res);
        }


        public override async Task<AccountMessage> CreateAccount(AccountMessage request, ServerCallContext context)
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

            var newAccount = _mapperMessageToModel.Map<AccountMessage, LinkUser>(request);

            //TODO: find better condition to use to see if newAccount already exists
            var res = await _accountRepository.GetAccountByEmailAsync(newAccount.EmailAddress);
            if (res == null)
            {
                try
                {
                    await _accountRepository.AddAsync(newAccount);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"CreateAccount exception: {ex.Message}");
                    throw;
                }
            }
            else
            {
                throw new RpcException(new Status(StatusCode.AlreadyExists, $"Account is already created for {res.Id}"));
            }

            _metrics.IncrementAccountAddedCounter([
                new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, newAccount.FacilityIds)
            ]);

            return _mapperModelToMessage.Map<LinkUser, AccountMessage>(newAccount);
        }

        public override async Task<AccountMessage> UpdateAccount(AccountMessage request, ServerCallContext context)
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

            var updatedAccount = _mapperMessageToModel.Map<AccountMessage, LinkUser>(request);

            if (updatedAccount.Id == Guid.Empty)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ID provided"));
            }

            LinkUser returnedModel;
            try
            {
                returnedModel = await _accountRepository.UpdateAsync(updatedAccount);
                if (returnedModel == null)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, $"No account found for {updatedAccount.Id}"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"UpdateAccount exception: {ex.Message}");
                throw new RpcException(new Status(StatusCode.Internal, $"UpdateAccount exception: {ex.Message}"));
            }


            return _mapperModelToMessage.Map<LinkUser, AccountMessage>(returnedModel);
        }

        public override async Task<AccountDeletedMessage> DeleteAccount(DeleteAccountMessage request, ServerCallContext context)
        {
            
            try
            {
                await _accountRepository.DeleteAsync(Guid.Parse(request.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError($"DeleteAccount exception: {ex.Message}");
                throw new RpcException(new Status(StatusCode.Internal, $"DeleteAccount exception: {ex.Message}"));
            }

            _metrics.IncrementAccountDeactivatedCounter([]);

            return new AccountDeletedMessage();
            
        }

        public override async Task<AccountMessage> RestoreAccount(RestoreAccountMessage request, ServerCallContext context)
        {

            LinkUser restoredAccount;
            try
            {
                restoredAccount = await _accountRepository.RestoreAsync(Guid.Parse(request.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError($"RestoreAccount exception: {ex.Message}");
                throw new RpcException(new Status(StatusCode.Internal, $"RestoreAccount exception: {ex.Message}"));
            }

            _metrics.IncrementAccountRestoredCounter([]);

            return _mapperModelToMessage.Map<LinkUser, AccountMessage>(restoredAccount);
        }


        #endregion


        #region Account group management

        public override async Task<AccountMessage> AddAccountToGroup(AddAccountToGroupMessage request, ServerCallContext context)
        {
            LinkUser account;
            try
            {
                account = await _accountRepository.AddAccountToGroup(Guid.Parse(request.AccountId), Guid.Parse(request.GroupId));
            }
            catch (Exception ex)
            {
                _logger.LogError($"AddAccountToGroup exception: {ex.Message}");
                throw new RpcException(new Status(StatusCode.Internal, $"AddAccountToGroup exception: {ex.Message}"));
            }


            return _mapperModelToMessage.Map<LinkUser, AccountMessage>(account);
        }

        public override async Task<AccountRemovedFromGroupMessage> RemoveAccountFromGroup(RemoveAccountFromGroupMessage request, ServerCallContext context)
        {
            try
            {
                await _accountRepository.RemoveAccountFromGroup(Guid.Parse(request.AccountId), Guid.Parse(request.GroupId));
            }
            catch (Exception ex)
            {
                _logger.LogError($"RemoveAccountFromGroup exception: {ex.Message}");
                throw new RpcException(new Status(StatusCode.Internal, $"RemoveAccountFromGroup exception: {ex.Message}"));
            }

            return new AccountRemovedFromGroupMessage();
        }

        #endregion




        #region Account role management

        public override async Task<AccountMessage> AddRoleToAccount(AddRoleToAccountMessage request, ServerCallContext context)
        {
            LinkUser account;
            try
            {
                account = await _accountRepository.AddRoleToAccount(Guid.Parse(request.AccountId), Guid.Parse(request.RoleId));
            }
            catch (Exception ex)
            {
                _logger.LogError($"AddRoleToAccount exception: {ex.Message}");
                throw new RpcException(new Status(StatusCode.Internal, $"AddRoleToAccount exception: {ex.Message}"));
            }

            return _mapperModelToMessage.Map<LinkUser, AccountMessage>(account);
        }

        public override async Task<RoleRemovedFromAccountMessage> RemoveRoleFromAccount(RemoveRoleFromAccountMessage request, ServerCallContext context)
        {

            try
            {
                await _accountRepository.RemoveRoleFromAccount(Guid.Parse(request.AccountId), Guid.Parse(request.RoleId));
            }
            catch (Exception ex)
            {
                _logger.LogError($"RemoveRoleFromAccount exception: {ex.Message}");
                throw new RpcException(new Status(StatusCode.Internal, $"RemoveRoleFromAccount exception: {ex.Message}"));
            }

            return new RoleRemovedFromAccountMessage();
        }

        #endregion



    }
}