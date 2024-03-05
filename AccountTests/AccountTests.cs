using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Repositories;
using Microsoft.EntityFrameworkCore;
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

        AccountTestsHelper _helper = new AccountTestsHelper();

        [Fact]
        public void TestGetAccountAsync()
        {
            _mocker = new AutoMocker();
            var accounts = new List<AccountModel>() { _helper.CreateTestAccount() };
            DbContextOptions options = new DbContextOptionsBuilder<TestDataContext>()
                .UseInMemoryDatabase("TestDatabase")
                .Options;

            //var logger = new Mock<ILogger<DataContext>>();
            //var kafkaConnection = new Mock<IOptions<KafkaConnection>>();
            //var postgresConnection = new Mock<IOptions<PostgresConnection>>();

            var testDataContext = new Mock<TestDataContext>(options);
            var dbSet = new Mock<DbSet<AccountModel>>();
            _helper.AccountDbSetSetup(dbSet, accounts);
            testDataContext.Setup(x => x.Accounts).Returns(dbSet.Object);
            _mocker.Use(testDataContext);

            var _accountRepository = _mocker.CreateInstance<AccountRepository>();
            var account = _accountRepository.GetAsync(accountId, false, CancellationToken.None).Result;

            Assert.NotNull(account);
        }

        [Fact]
        public void TestGetAccountByEmailAsync()
        {
            _mocker = new AutoMocker();
            var accounts = new List<AccountModel>() { _helper.CreateTestAccount() };
            DbContextOptions options = new DbContextOptionsBuilder<TestDataContext>()
                .UseInMemoryDatabase("TestDatabase")
                .Options;

            var testDataContext = new Mock<TestDataContext>(options);
            var dbSet = new Mock<DbSet<AccountModel>>();
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
            var accounts = new List<AccountModel>() { accountModel };
            var groups = new List<GroupModel>() { groupModel };
            DbContextOptions options = new DbContextOptionsBuilder<TestDataContext>()
                .UseInMemoryDatabase("TestDatabase")
                .Options;
            var testDataContext = new Mock<TestDataContext>(options);
            _mocker.Use(testDataContext.Object);

            var _accountRepository = _mocker.CreateInstance<AccountRepository>();

            var accounDbSet = new Mock<DbSet<AccountModel>>();
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
        public void TestRemoveAccountFromGroup()
        {
            _mocker = new AutoMocker();

            var accountModel = _helper.CreateTestAccount();
            var groupModel = _helper.CreateTestGroup();
            var accounts = new List<AccountModel>() { accountModel };
            var groups = new List<GroupModel>() { groupModel };
            accountModel.Groups.Add(groupModel);
            DbContextOptions options = new DbContextOptionsBuilder<TestDataContext>()
                .UseInMemoryDatabase("TestDatabase")
                .Options;
            var testDataContext = new Mock<TestDataContext>(options);
            _mocker.Use(testDataContext.Object);

            var accountDbSet = new Mock<DbSet<AccountModel>>();
            var groupDbSet = new Mock<DbSet<GroupModel>>();
            _helper.AccountDbSetSetup(accountDbSet, accounts);
            _helper.GroupDbSetSetup(groupDbSet, groups);
            testDataContext.Setup(x => x.Accounts).Returns(accountDbSet.Object);
            testDataContext.Setup(x => x.Groups).Returns(groupDbSet.Object);

            var _accountRepository = _mocker.CreateInstance<AccountRepository>();

            var account = _accountRepository.RemoveAccountFromGroup(accountId, groupId);
            Assert.DoesNotContain(groupModel, accountModel.Groups);
            testDataContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void TestAddRoleToAccount()
        {
            _mocker = new AutoMocker();
            var accountModel = _helper.CreateTestAccount();
            var roleModel = _helper.CreateTestRole();
            var accounts = new List<AccountModel>() { accountModel };
            var roles = new List<RoleModel>() { roleModel };
            DbContextOptions options = new DbContextOptionsBuilder<TestDataContext>()
                .UseInMemoryDatabase("TestDatabase")
                .Options;
            var testDataContext = new Mock<TestDataContext>(options);
            _mocker.Use(testDataContext.Object);

            var _accountRepository = _mocker.CreateInstance<AccountRepository>();

            var accounDbSet = new Mock<DbSet<AccountModel>>();
            var roleDbSet = new Mock<DbSet<RoleModel>>();
            _helper.AccountDbSetSetup(accounDbSet, accounts);
            _helper.RoleDbSetSetup(roleDbSet, roles);
            testDataContext.Setup(x => x.Accounts).Returns(accounDbSet.Object);
            testDataContext.Setup(x => x.Roles).Returns(roleDbSet.Object);

            var account = _accountRepository.AddRoleToAccount(accountId, roleId).Result;
            Assert.NotNull(account);
            Assert.Contains(roleModel, account.Roles);
        }

        [Fact]
        public void TestRemoveRoleFromAccount()
        {
            _mocker = new AutoMocker();

            var accountModel = _helper.CreateTestAccount();
            var roleModel = _helper.CreateTestRole();
            var accounts = new List<AccountModel>() { accountModel };
            var roles = new List<RoleModel>() { roleModel };
            accountModel.Roles.Add(roleModel);
            DbContextOptions options = new DbContextOptionsBuilder<TestDataContext>()
                .UseInMemoryDatabase("TestDatabase")
                .Options;
            var testDataContext = new Mock<TestDataContext>(options);
            _mocker.Use(testDataContext.Object);

            var accounDbSet = new Mock<DbSet<AccountModel>>();
            var roleDbSet = new Mock<DbSet<RoleModel>>();
            _helper.AccountDbSetSetup(accounDbSet, accounts);
            _helper.RoleDbSetSetup(roleDbSet, roles);
            testDataContext.Setup(x => x.Accounts).Returns(accounDbSet.Object);
            testDataContext.Setup(x => x.Roles).Returns(roleDbSet.Object);

            var _accountRepository = _mocker.CreateInstance<AccountRepository>();

            var account = _accountRepository.RemoveRoleFromAccount(accountId, roleId);
            Assert.DoesNotContain(roleModel, accountModel.Roles);
            testDataContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void TestGetGroupAsync()
        {
            _mocker = new AutoMocker();
            var groups = new List<GroupModel>() { _helper.CreateTestGroup() };
            DbContextOptions options = new DbContextOptionsBuilder<TestDataContext>()
                .UseInMemoryDatabase("TestDatabase")
                .Options;

            var testDataContext = new Mock<TestDataContext>(options);
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
            var roles = new List<RoleModel>() { roleModel };
            DbContextOptions options = new DbContextOptionsBuilder<TestDataContext>()
                .UseInMemoryDatabase("TestDatabase")
                .Options;
            var testDataContext = new Mock<TestDataContext>(options);
            _mocker.Use(testDataContext.Object);

            var _groupRepository = _mocker.CreateInstance<GroupRepository>();

            var groupDbSet = new Mock<DbSet<GroupModel>>();
            var roleDbSet = new Mock<DbSet<RoleModel>>();
            _helper.GroupDbSetSetup(groupDbSet, groups);
            _helper.RoleDbSetSetup(roleDbSet, roles);
            testDataContext.Setup(x => x.Groups).Returns(groupDbSet.Object);
            testDataContext.Setup(x => x.Roles).Returns(roleDbSet.Object);

            var group = _groupRepository.AddRoleToGroup(groupId, roleId).Result;
            Assert.NotNull(group);
            Assert.Contains(roleModel, groupModel.Roles);
        }

        [Fact]
        public void TestRemoveRoleFromGroup()
        {
            _mocker = new AutoMocker();

            var groupModel = _helper.CreateTestGroup();
            var roleModel = _helper.CreateTestRole();
            var groups = new List<GroupModel>() { groupModel };
            var roles = new List<RoleModel>() { roleModel };
            groupModel.Roles.Add(roleModel);
            DbContextOptions options = new DbContextOptionsBuilder<TestDataContext>()
                .UseInMemoryDatabase("TestDatabase")
                .Options;
            var testDataContext = new Mock<TestDataContext>(options);
            _mocker.Use(testDataContext.Object);

            var groupDbSet = new Mock<DbSet<GroupModel>>();
            var roleDbSet = new Mock<DbSet<RoleModel>>();
            _helper.GroupDbSetSetup(groupDbSet, groups);
            _helper.RoleDbSetSetup(roleDbSet, roles);
            testDataContext.Setup(x => x.Groups).Returns(groupDbSet.Object);
            testDataContext.Setup(x => x.Roles).Returns(roleDbSet.Object);

            var _groupRepository = _mocker.CreateInstance<GroupRepository>();

            var account = _groupRepository.RemoveRoleFromGroup(groupId, roleId);
            Assert.DoesNotContain(roleModel, groupModel.Roles);
            testDataContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
