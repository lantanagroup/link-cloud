using AutoMapper;
using Grpc.Core;
using LantanaGroup.Link.Account.Converters;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Protos;
using LantanaGroup.Link.Account.Repositories;
using LantanaGroup.Link.Shared.Application.Converters;

namespace LantanaGroup.Link.Account.Services
{

    public class AccountService : Protos.AccountService.AccountServiceBase
    {
        private readonly ILogger<AccountService> _logger;
        private readonly AccountRepository _accountRepository;

        private readonly IMapper _mapperModelToMessage;
        private readonly IMapper _mapperMessageToModel;

        public AccountService(ILogger<AccountService> logger, AccountRepository accountRepository)
        {
            _logger = logger;
            _accountRepository = accountRepository;

            _mapperModelToMessage = new ProtoMessageMapper<AccountModel, AccountMessage>(new AccountProfile()).CreateMapper();
            _mapperMessageToModel = new ProtoMessageMapper<AccountMessage, AccountModel>(new AccountProfile()).CreateMapper();
        }


        #region Basic CRUD

        public override async Task GetAllAccounts(GetAllAccountsMessage request, IServerStreamWriter<AccountMessage> responseStream, ServerCallContext context)
        {
            var res = await _accountRepository.GetAllAsync();

            foreach (var account in res)
            {
                var message = _mapperModelToMessage.Map<AccountModel, AccountMessage>(account);
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

            return _mapperModelToMessage.Map<AccountModel, AccountMessage>(res);
        }


        public override async Task<AccountMessage> CreateAccount(AccountMessage request, ServerCallContext context)
        {
            var newAccount = _mapperMessageToModel.Map<AccountMessage, AccountModel>(request);

            //TODO: find better condtion to use to see if newAccount already exists
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

            return _mapperModelToMessage.Map<AccountModel, AccountMessage>(newAccount);
        }

        public override async Task<AccountMessage> UpdateAccount(AccountMessage request, ServerCallContext context)
        {
            var updatedAccount = _mapperMessageToModel.Map<AccountMessage, AccountModel>(request);

            if (updatedAccount.Id == Guid.Empty)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ID provided"));
            }

            AccountModel returnedModel;
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


            return _mapperModelToMessage.Map<AccountModel, AccountMessage>(returnedModel);
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

            return new AccountDeletedMessage();
            
        }

        public override async Task<AccountMessage> RestoreAccount(RestoreAccountMessage request, ServerCallContext context)
        {

            AccountModel restoredAccount;
            try
            {
                restoredAccount = await _accountRepository.RestoreAsync(Guid.Parse(request.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError($"RestoreAccount exception: {ex.Message}");
                throw new RpcException(new Status(StatusCode.Internal, $"RestoreAccount exception: {ex.Message}"));
            }

            return _mapperModelToMessage.Map<AccountModel, AccountMessage>(restoredAccount);
        }


        #endregion


        #region Account group management

        public override async Task<AccountMessage> AddAccountToGroup(AddAccountToGroupMessage request, ServerCallContext context)
        {
            AccountModel account;
            try
            {
                account = await _accountRepository.AddAccountToGroup(Guid.Parse(request.AccountId), Guid.Parse(request.GroupId));
            }
            catch (Exception ex)
            {
                _logger.LogError($"AddAccountToGroup exception: {ex.Message}");
                throw new RpcException(new Status(StatusCode.Internal, $"AddAccountToGroup exception: {ex.Message}"));
            }


            return _mapperModelToMessage.Map<AccountModel, AccountMessage>(account);
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
            AccountModel account;
            try
            {
                account = await _accountRepository.AddRoleToAccount(Guid.Parse(request.AccountId), Guid.Parse(request.RoleId));
            }
            catch (Exception ex)
            {
                _logger.LogError($"AddRoleToAccount exception: {ex.Message}");
                throw new RpcException(new Status(StatusCode.Internal, $"AddRoleToAccount exception: {ex.Message}"));
            }

            return _mapperModelToMessage.Map<AccountModel, AccountMessage>(account);
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