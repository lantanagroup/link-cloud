using Moq.AutoMock;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Account
{
    [Collection("UnitTests")]
    public class AccountTests
    {
        private string accountId = AccountTestsConstants.accountId;
        private string roleId = AccountTestsConstants.roleId;
        private AutoMocker? _mocker;

        private readonly AccountTestsHelper _helper = new AccountTestsHelper();


        //TODO UNIT TEST

        public async Task TestGetAccountAsync()
        {
            //_mocker = new AutoMocker();
            //var accounts = new List<LinkUser>() { _helper.CreateTestAccount() };
            //DbContextOptions options = new DbContextOptionsBuilder<AccountDbContext>()
            //    .UseInMemoryDatabase("TestDatabase")
            //    .Options;

            //var mockLogger = new Mock<ILogger<AccountDbContext>>();
            //var mockKafka = new Mock<IOptions<KafkaConnection>>();
            //var mockSettings = new Mock<IOptions<AccountDbContext>>();

            //var testDataContext = new Mock<AccountDbContext>(mockLogger.Object, mockKafka.Object, mockSettings.Object, options);
            //var dbSet = new Mock<DbSet<LinkUser>>();
            //_helper.AccountDbSetSetup(dbSet, accounts);

            //testDataContext.Setup(x => x.Users).Returns(dbSet.Object);
            //_mocker.Use(testDataContext);

            //var accountRepository = _mocker.CreateInstance<IUserRepository>();
            //var account = await accountRepository.GetUserAsync(accountId, false, CancellationToken.None);

            //Assert.NotNull(account);
        }

        public async Task TestGetAccountByEmailAsync()
        {
            //_mocker = new AutoMocker();          

            //var accounts = new List<LinkUser>() { _helper.CreateTestAccount() };
            //DbContextOptions options = new DbContextOptionsBuilder<AccountDbContext>()
            //    .UseInMemoryDatabase("TestDatabase")
            //    .Options;

            //_mocker.GetMock<IUserRepository>()
            //    .Setup(x => x.GetUserByEmailAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            //    .ReturnsAsync(_helper.CreateTestAccount());

            //var mockLogger = new Mock<ILogger<AccountDbContext>>();         
            //var testDataContext = new Mock<AccountDbContext>();
            //var dbSet = new Mock<DbSet<LinkUser>>();
            //_helper.AccountDbSetSetup(dbSet, accounts);   
            
            //_mocker.Use(testDataContext);

            //var _accountRepository = _mocker.CreateInstance<IUserRepository>();
            //var account = await _accountRepository.GetUserByEmailAsync(AccountTestsConstants.email, false, CancellationToken.None);

            //Assert.NotNull(account);
        }    

    }
}
