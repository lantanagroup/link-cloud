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

        public LinkUser CreateTestAccount()
        {
            return new LinkUser()
            {
                Id = new Guid(AccountTestsConstants.accountId),
                Username = "testUser",
                EmailAddress = AccountTestsConstants.email,
                FirstName = "Tester",
                LastName = "McTesterson",
                Groups = new List<GroupModel>(),
                Roles = new List<LinkRole>(),
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

        public LinkRole CreateTestRole()
        {
            return new LinkRole()
            {
                Id = new Guid(AccountTestsConstants.roleId)
            };
        }

        public void AccountDbSetSetup(Mock<DbSet<LinkUser>> dbSet, List<LinkUser> accounts)
        {
            dbSet.As<IAsyncEnumerable<LinkUser>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
                .Returns(new TestAsyncEnumerator<LinkUser>(accounts.GetEnumerator()));

            dbSet.As<IQueryable<LinkUser>>().Setup(m => m.Provider)
                .Returns(new TestAsyncQueryProvider<LinkUser>(accounts.AsQueryable().Provider));
            dbSet.As<IQueryable<LinkUser>>().Setup(m => m.Expression).Returns(accounts.AsQueryable().Expression);
            dbSet.As<IQueryable<LinkUser>>().Setup(m => m.ElementType).Returns(accounts.AsQueryable().ElementType);
            dbSet.As<IQueryable<LinkUser>>().Setup(m => m.GetEnumerator()).Returns(accounts.GetEnumerator());
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

        public void RoleDbSetSetup(Mock<DbSet<LinkRole>> dbSet, List<LinkRole> roles)
        {
            dbSet.As<IAsyncEnumerable<LinkRole>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
                .Returns(new TestAsyncEnumerator<LinkRole>(roles.GetEnumerator()));

            dbSet.As<IQueryable<LinkRole>>().Setup(m => m.Provider)
                .Returns(new TestAsyncQueryProvider<LinkRole>(roles.AsQueryable().Provider));
            dbSet.As<IQueryable<LinkRole>>().Setup(m => m.Expression).Returns(roles.AsQueryable().Expression);
            dbSet.As<IQueryable<LinkRole>>().Setup(m => m.ElementType).Returns(roles.AsQueryable().ElementType);
            dbSet.As<IQueryable<LinkRole>>().Setup(m => m.GetEnumerator()).Returns(roles.GetEnumerator());
        }
    }
}
