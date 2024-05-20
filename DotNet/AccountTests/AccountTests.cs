using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Persistence;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;

namespace AccountUnitTests
{
    public class AccountTests
    {
        private string accountId = AccountTestsConstants.accountId;
        private string roleId = AccountTestsConstants.roleId;
        private AutoMocker? _mocker;

        private readonly AccountTestsHelper _helper = new AccountTestsHelper();

        [Fact]
        public async Task TestGetAccountAsync()
        {
            _mocker = new AutoMocker();
            var accounts = new List<LinkUser>() { _helper.CreateTestAccount() };
            DbContextOptions options = new DbContextOptionsBuilder<AccountDbContext>()
                .UseInMemoryDatabase("TestDatabase")
                .Options;

            var mockLogger = new Mock<ILogger<AccountDbContext>>();
            var mockKafka = new Mock<IOptions<KafkaConnection>>();
            var mockSettings = new Mock<IOptions<AccountDbContext>>();

            var testDataContext = new Mock<AccountDbContext>(mockLogger.Object, mockKafka.Object, mockSettings.Object, options);
            var dbSet = new Mock<DbSet<LinkUser>>();
            _helper.AccountDbSetSetup(dbSet, accounts);

            testDataContext.Setup(x => x.Users).Returns(dbSet.Object);
            _mocker.Use(testDataContext);

            var accountRepository = _mocker.CreateInstance<IUserRepository>();
            var account = await accountRepository.GetUserAsync(accountId, false, CancellationToken.None);

            Assert.NotNull(account);
        }

        [Fact]
        public async Task TestGetAccountByEmailAsync()
        {
            _mocker = new AutoMocker();
            var accounts = new List<LinkUser>() { _helper.CreateTestAccount() };
            DbContextOptions options = new DbContextOptionsBuilder<AccountDbContext>()
                .UseInMemoryDatabase("TestDatabase")
                .Options;

            var mockLogger = new Mock<ILogger<AccountDbContext>>();
            var mockKafka = new Mock<IOptions<KafkaConnection>>();

            var testDataContext = new Mock<AccountDbContext>();
            var dbSet = new Mock<DbSet<LinkUser>>();
            _helper.AccountDbSetSetup(dbSet, accounts);
            testDataContext.Setup(x => x.Users).Returns(dbSet.Object);
            _mocker.Use(testDataContext);

            var _accountRepository = _mocker.CreateInstance<IUserRepository>();
            var account = await _accountRepository.GetUserByEmailAsync(AccountTestsConstants.email, false, CancellationToken.None);

            Assert.NotNull(account);
        }     

        //TODO UNIT TEST
        
    }
}
