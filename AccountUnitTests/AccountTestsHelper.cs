using Hl7.Fhir.Model;
using LantanaGroup.Link.Account.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccountUnitTests
{
    public class AccountTestsHelper
    {
        public AccountTestsHelper() { }

        public AccountModel CreateTestAccount()
        {
            return new AccountModel()
            {
                Id = new Guid(AccountTestsConstants.accountId),
                Username = "testUser",
                EmailAddress = AccountTestsConstants.email,
                FirstName = "Tester",
                LastName = "McTesterson",
                Groups = new List<GroupModel>(),
                Roles = new List<RoleModel>(),
                LastSeen = DateTime.Now
            };
        }

        public GroupModel CreateTestGroup()
        {
            return new GroupModel()
            {
                Id = new Guid(AccountTestsConstants.groupId)
            };
        }

        public RoleModel CreateTestRole()
        {
            return new RoleModel()
            {
                Id = new Guid(AccountTestsConstants.roleId)
            };
        }

        public void AccountDbSetSetup(Mock<DbSet<AccountModel>> dbSet, List<AccountModel> accounts)
        {
            dbSet.As<IAsyncEnumerable<AccountModel>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
                .Returns(new TestAsyncEnumerator<AccountModel>(accounts.GetEnumerator()));

            dbSet.As<IQueryable<AccountModel>>().Setup(m => m.Provider)
                .Returns(new TestAsyncQueryProvider<AccountModel>(accounts.AsQueryable().Provider));
            dbSet.As<IQueryable<AccountModel>>().Setup(m => m.Expression).Returns(accounts.AsQueryable().Expression);
            dbSet.As<IQueryable<AccountModel>>().Setup(m => m.ElementType).Returns(accounts.AsQueryable().ElementType);
            dbSet.As<IQueryable<AccountModel>>().Setup(m => m.GetEnumerator()).Returns(accounts.GetEnumerator());
        }

        public void GroupDbSetSetup(Mock<DbSet<GroupModel>> dbSet, List<GroupModel> groups)
        {
            dbSet.As<IAsyncEnumerable<GroupModel>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
                .Returns(new TestAsyncEnumerator<GroupModel>(groups.GetEnumerator()));

            dbSet.As<IQueryable<GroupModel>>().Setup(m => m.Provider)
                .Returns(new TestAsyncQueryProvider<GroupModel>(groups.AsQueryable().Provider));
            dbSet.As<IQueryable<GroupModel>>().Setup(m => m.Expression).Returns(groups.AsQueryable().Expression);
            dbSet.As<IQueryable<GroupModel>>().Setup(m => m.ElementType).Returns(groups.AsQueryable().ElementType);
            dbSet.As<IQueryable<GroupModel>>().Setup(m => m.GetEnumerator()).Returns(groups.GetEnumerator());
        }

        public void RoleDbSetSetup(Mock<DbSet<RoleModel>> dbSet, List<RoleModel> roles)
        {
            dbSet.As<IAsyncEnumerable<RoleModel>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
                .Returns(new TestAsyncEnumerator<RoleModel>(roles.GetEnumerator()));

            dbSet.As<IQueryable<RoleModel>>().Setup(m => m.Provider)
                .Returns(new TestAsyncQueryProvider<RoleModel>(roles.AsQueryable().Provider));
            dbSet.As<IQueryable<RoleModel>>().Setup(m => m.Expression).Returns(roles.AsQueryable().Expression);
            dbSet.As<IQueryable<RoleModel>>().Setup(m => m.ElementType).Returns(roles.AsQueryable().ElementType);
            dbSet.As<IQueryable<RoleModel>>().Setup(m => m.GetEnumerator()).Returns(roles.GetEnumerator());
        }
    }
}
