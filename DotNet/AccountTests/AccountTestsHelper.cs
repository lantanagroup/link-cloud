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
                Id = Guid.Parse(AccountTestsConstants.accountId),
                UserName = "testUser",
                Email = AccountTestsConstants.email,
                FirstName = "Tester",
                LastName = "McTesterson",               
                UserRoles = new List<LinkUserRole>(),
                LastSeen = DateTime.Now
            };
        }       

        public LinkRole CreateTestRole()
        {
            return new LinkRole()
            {
                Id = Guid.Parse(AccountTestsConstants.roleId),
                Name = "TestRole"
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
