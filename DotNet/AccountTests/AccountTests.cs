using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Repositories;
using LantanaGroup.Link.Account.Settings;
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
        private Guid accountId = new Guid(AccountTestsConstants.accountId);
        private Guid groupId = new Guid(AccountTestsConstants.groupId);
        private Guid roleId = new Guid(AccountTestsConstants.roleId);
        private AutoMocker? _mocker;

        private readonly AccountTestsHelper _helper = new AccountTestsHelper();

        [Fact]
        public async Task TestGetAccountAsync()
        {
            _mocker = new AutoMocker();
            var accounts = new List<LinkUser>() { _helper.CreateTestAccount() };
            DbContextOptions options = new DbContextOptionsBuilder<DataContext>()
                .UseInMemoryDatabase("TestDatabase")
                .Options;

            var mockLogger = new Mock<ILogger<DataContext>>();
            var mockKafka = new Mock<IOptions<KafkaConnection>>();
            var mockSettings = new Mock<IOptions<PostgresConnection>>();

            var testDataContext = new Mock<DataContext>(mockLogger.Object, mockKafka.Object, mockSettings.Object, options);
            var dbSet = new Mock<DbSet<LinkUser>>();
            _helper.AccountDbSetSetup(dbSet, accounts);

            testDataContext.Setup(x => x.Accounts).Returns(dbSet.Object);
            _mocker.Use(testDataContext);

            var accountRepository = _mocker.CreateInstance<AccountRepository>();
            var account = await accountRepository.GetAsync(accountId, false, CancellationToken.None);

            Assert.NotNull(account);
        }

        [Fact]
        public void TestGetAccountByEmailAsync()
        {
            _mocker = new AutoMocker();
            var accounts = new List<LinkUser>() { _helper.CreateTestAccount() };
            DbContextOptions options = new DbContextOptionsBuilder<DataContext>()
                .UseInMemoryDatabase("TestDatabase")
                .Options;

            var mockLogger = new Mock<ILogger<DataContext>>();
            var mockKafka = new Mock<IOptions<KafkaConnection>>();
            var mockSettings = new Mock<IOptions<PostgresConnection>>();

            var testDataContext = new Mock<DataContext>(mockLogger.Object, mockKafka.Object, mockSettings.Object, options);
            var dbSet = new Mock<DbSet<LinkUser>>();
            _helper.AccountDbSetSetup(dbSet, accounts);
            testDataContext.Setup(x => x.Accounts).Returns(dbSet.Object);
            _mocker.Use(testDataContext);

            var _accountRepository = _mocker.CreateInstance<AccountRepository>();
            var account = _accountRepository.GetAccountByEmailAsync(AccountTestsConstants.email, false, CancellationToken.None).Result;

            Assert.NotNull(account);
        }

        [Fact]
        public void TestAddAccountToGroup()
        {
            _mocker = new AutoMocker();
            var accountModel = _helper.CreateTestAccount();
            var groupModel = _helper.CreateTestGroup();
            var accounts = new List<LinkUser>() { accountModel };
            var groups = new List<GroupModel>() { groupModel };
            DbContextOptions options = new DbContextOptionsBuilder<DataContext>()
                .UseInMemoryDatabase("TestDatabase")
                .Options;

            var mockLogger = new Mock<ILogger<DataContext>>();
            var mockKafka = new Mock<IOptions<KafkaConnection>>();
            var mockSettings = new Mock<IOptions<PostgresConnection>>();

            var testDataContext = new Mock<DataContext>(mockLogger.Object, mockKafka.Object, mockSettings.Object, options);

            _mocker.Use(testDataContext.Object);

            var _accountRepository = _mocker.CreateInstance<AccountRepository>();

            var accounDbSet = new Mock<DbSet<LinkUser>>();
            var groupDbSet = new Mock<DbSet<GroupModel>>();
            _helper.AccountDbSetSetup(accounDbSet, accounts);
            _helper.GroupDbSetSetup(groupDbSet, groups);
            testDataContext.Setup(x => x.Accounts).Returns(accounDbSet.Object);
            testDataContext.Setup(x => x.Groups).Returns(groupDbSet.Object);

            var account = _accountRepository.AddAccountToGroup(accountId, groupId).Result;
            Assert.NotNull(account);
            Assert.Contains(groupModel, account.Groups);
        }

        [Fact]
        public async Task TestRemoveAccountFromGroup()
        {
            _mocker = new AutoMocker();

            var accountModel = _helper.CreateTestAccount();
            var groupModel = _helper.CreateTestGroup();
            var groups = new List<GroupModel>() { groupModel };
            accountModel.Groups.Add(groupModel);

            var accounts = new List<LinkUser>() { accountModel };
            DbContextOptions options = new DbContextOptionsBuilder<DataContext>()
                .UseInMemoryDatabase("TestDatabase")
                .Options;

            var mockLogger = new Mock<ILogger<DataContext>>();
            var mockKafka = new Mock<IOptions<KafkaConnection>>();
            var mockSettings = new Mock<IOptions<PostgresConnection>>();

            var testDataContext = new Mock<DataContext>(mockLogger.Object, mockKafka.Object, mockSettings.Object, options);

            var accountDbSet = new Mock<DbSet<LinkUser>>();
            var groupDbSet = new Mock<DbSet<GroupModel>>();
            _helper.AccountDbSetSetup(accountDbSet, accounts);
            _helper.GroupDbSetSetup(groupDbSet, groups);
            testDataContext.Setup(x => x.Accounts).Returns(accountDbSet.Object);
            testDataContext.Setup(x => x.Groups).Returns(groupDbSet.Object);

            _mocker.Use(testDataContext);

            var _accountRepository = _mocker.CreateInstance<AccountRepository>();

            await _accountRepository.RemoveAccountFromGroup(accountId, groupId);

            Assert.DoesNotContain(groupModel, accountModel.Groups);

            testDataContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task TestAddRoleToAccount()
        {
            _mocker = new AutoMocker();
            var accountModel = _helper.CreateTestAccount();
            var roleModel = _helper.CreateTestRole();
            var accounts = new List<LinkUser>() { accountModel };
            var roles = new List<LinkRole>() { roleModel };
            DbContextOptions options = new DbContextOptionsBuilder<DataContext>()
                .UseInMemoryDatabase("TestDatabase")
                .Options;

            var mockLogger = new Mock<ILogger<DataContext>>();
            var mockKafka = new Mock<IOptions<KafkaConnection>>();
            var mockSettings = new Mock<IOptions<PostgresConnection>>();

            var testDataContext = new Mock<DataContext>(mockLogger.Object, mockKafka.Object, mockSettings.Object, options);

            var accountDbSet = new Mock<DbSet<LinkUser>>();
            var roleDbSet = new Mock<DbSet<LinkRole>>();
            _helper.AccountDbSetSetup(accountDbSet, accounts);
            _helper.RoleDbSetSetup(roleDbSet, roles);
            testDataContext.Setup(x => x.Accounts).Returns(accountDbSet.Object);
            testDataContext.Setup(x => x.Roles).Returns(roleDbSet.Object);

            _mocker.Use(testDataContext.Object);

            var _accountRepository = _mocker.CreateInstance<AccountRepository>();

            var account = await _accountRepository.AddRoleToAccount(accountId, roleId);

            Assert.NotNull(account);
            Assert.Contains(roleModel, account.Roles);
        }

        [Fact]
        public async Task TestRemoveRoleFromAccount()
        {
            _mocker = new AutoMocker();

            var accountModel = _helper.CreateTestAccount();
            var roleModel = _helper.CreateTestRole();
            var accounts = new List<LinkUser>() { accountModel };
            var roles = new List<LinkRole>() { roleModel };
            accountModel.Roles.Add(roleModel);
            DbContextOptions options = new DbContextOptionsBuilder<DataContext>()
                .UseInMemoryDatabase("TestDatabase")
                .Options;

            var mockLogger = new Mock<ILogger<DataContext>>();
            var mockKafka = new Mock<IOptions<KafkaConnection>>();
            var mockSettings = new Mock<IOptions<PostgresConnection>>();

            var testDataContext = new Mock<DataContext>(mockLogger.Object, mockKafka.Object, mockSettings.Object, options);

            var accountDbSet = new Mock<DbSet<LinkUser>>();
            var roleDbSet = new Mock<DbSet<LinkRole>>();
            _helper.AccountDbSetSetup(accountDbSet, accounts);
            _helper.RoleDbSetSetup(roleDbSet, roles);
            testDataContext.Setup(x => x.Accounts).Returns(accountDbSet.Object);
            testDataContext.Setup(x => x.Roles).Returns(roleDbSet.Object);

            _mocker.Use(testDataContext.Object);
            var accountRepository = _mocker.CreateInstance<AccountRepository>();

            await accountRepository.RemoveRoleFromAccount(accountId, roleId);
            Assert.DoesNotContain(roleModel, accountModel.Roles);
            testDataContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void TestGetGroupAsync()
        {
            _mocker = new AutoMocker();
            var groups = new List<GroupModel>() { _helper.CreateTestGroup() };
            DbContextOptions options = new DbContextOptionsBuilder<DataContext>()
                .UseInMemoryDatabase("TestDatabase")
                .Options;

            var mockLogger = new Mock<ILogger<DataContext>>();
            var mockKafka = new Mock<IOptions<KafkaConnection>>();
            var mockSettings = new Mock<IOptions<PostgresConnection>>();

            var testDataContext = new Mock<DataContext>(mockLogger.Object, mockKafka.Object, mockSettings.Object, options);

            var dbSet = new Mock<DbSet<GroupModel>>();
            _helper.GroupDbSetSetup(dbSet, groups);
            testDataContext.Setup(x => x.Groups).Returns(dbSet.Object);

            _mocker.Use(testDataContext);

            var _groupRepository = _mocker.CreateInstance<GroupRepository>();
            var group = _groupRepository.GetAsync(groupId, false, CancellationToken.None).Result;

            Assert.NotNull(group);
        }

        [Fact]
        public void TestAddRoleToGroup()
        {
            _mocker = new AutoMocker();
            var groupModel = _helper.CreateTestGroup();
            var roleModel = _helper.CreateTestRole();
            var groups = new List<GroupModel>() { groupModel };
            var roles = new List<LinkRole>() { roleModel };

            DbContextOptions options = new DbContextOptionsBuilder<DataContext>()
                .UseInMemoryDatabase("TestDatabase")
                .Options;

            var mockLogger = new Mock<ILogger<DataContext>>();
            var mockKafka = new Mock<IOptions<KafkaConnection>>();
            var mockSettings = new Mock<IOptions<PostgresConnection>>();

            var testDataContext = new Mock<DataContext>(mockLogger.Object, mockKafka.Object, mockSettings.Object, options);

            var groupDbSet = new Mock<DbSet<GroupModel>>();
            var roleDbSet = new Mock<DbSet<LinkRole>>();
            _helper.GroupDbSetSetup(groupDbSet, groups);
            _helper.RoleDbSetSetup(roleDbSet, roles);
            testDataContext.Setup(x => x.Groups).Returns(groupDbSet.Object);
            testDataContext.Setup(x => x.Roles).Returns(roleDbSet.Object);

            _mocker.Use(testDataContext);
            var groupRepository = _mocker.CreateInstance<GroupRepository>();

            var group = groupRepository.AddRoleToGroup(groupId, roleId).Result;
            Assert.NotNull(group);
            Assert.Contains(roleModel, groupModel.Roles);
        }

        [Fact]
        public async Task TestRemoveRoleFromGroup()
        {
            _mocker = new AutoMocker();

            var groupModel = _helper.CreateTestGroup();
            var roleModel = _helper.CreateTestRole();
            var groups = new List<GroupModel>() { groupModel };
            var roles = new List<LinkRole>() { roleModel };
            groupModel.Roles.Add(roleModel);
            DbContextOptions options = new DbContextOptionsBuilder<DataContext>()
                .UseInMemoryDatabase("TestDatabase")
                .Options;

            var mockLogger = new Mock<ILogger<DataContext>>();
            var mockKafka = new Mock<IOptions<KafkaConnection>>();
            var mockSettings = new Mock<IOptions<PostgresConnection>>();

            var testDataContext = new Mock<DataContext>(mockLogger.Object, mockKafka.Object, mockSettings.Object, options);

            var groupDbSet = new Mock<DbSet<GroupModel>>();
            var roleDbSet = new Mock<DbSet<LinkRole>>();
            _helper.GroupDbSetSetup(groupDbSet, groups);
            _helper.RoleDbSetSetup(roleDbSet, roles);
            testDataContext.Setup(x => x.Groups).Returns(groupDbSet.Object);
            testDataContext.Setup(x => x.Roles).Returns(roleDbSet.Object);

            _mocker.Use(testDataContext.Object);

            var groupRepository = _mocker.CreateInstance<GroupRepository>();

            await groupRepository.RemoveRoleFromGroup(groupId, roleId);

            Assert.DoesNotContain(roleModel, groupModel.Roles);
            testDataContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
