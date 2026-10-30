using LantanaGroup.Link.Shared.Application.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared.Auditing
{
    /// <summary>
    /// A captured change counts only once the database has kept it. A save that fails, or a
    /// transaction that rolls back or is abandoned, contributes nothing - which is what lets a
    /// retrying execution strategy run the same unit of work twice without the event describing it
    /// twice.
    /// </summary>
    [Trait("Category", "UnitTests")]
    public class AuditPropertyChangeTransactionTests
    {
        private static (AuditInterceptorFixture Fixture, AuditContext Audit, TestVendor Vendor) Build()
        {
            var audit = new AuditContext { IsRecording = true };
            var interceptor = new AuditPropertyChangeInterceptor(audit, AuditTest.Options().AsOptions(),
                NullLogger<AuditPropertyChangeInterceptor>.Instance);
            var fixture = new AuditInterceptorFixture(interceptor);

            var vendor = new TestVendor { Name = "Epic" };
            fixture.Context.Vendors.Add(vendor);
            fixture.Context.SaveChanges();

            return (fixture, audit, vendor);
        }

        private static async Task<TestVendor> Reload(AuditInterceptorFixture fixture, string id)
        {
            fixture.Context.ChangeTracker.Clear();
            return await fixture.Context.Vendors.SingleAsync(v => v.Id == id);
        }

        [Fact]
        public async Task AChangeSavedInsideATransaction_IsNotRecordedUntilItCommits()
        {
            var (fixture, audit, vendor) = Build();
            using var _ = fixture;

            await using var transaction = await fixture.Context.Database.BeginTransactionAsync();
            vendor.Name = "Cerner";
            await fixture.Context.SaveChangesAsync();

            Assert.Empty(audit.PropertyChanges);

            await transaction.CommitAsync();

            var change = Assert.Single(audit.PropertyChanges);
            Assert.Equal("Epic", change.InitialPropertyValue);
            Assert.Equal("Cerner", change.NewPropertyValue);
        }

        [Fact]
        public async Task AChangeSavedInsideARolledBackTransaction_IsNeverRecorded()
        {
            var (fixture, audit, vendor) = Build();
            using var _ = fixture;

            await using var transaction = await fixture.Context.Database.BeginTransactionAsync();
            vendor.Name = "Cerner";
            await fixture.Context.SaveChangesAsync();
            await transaction.RollbackAsync();

            Assert.Empty(audit.PropertyChanges);
        }

        [Fact]
        public async Task ASaveThatFails_RecordsNothing()
        {
            var (fixture, audit, vendor) = Build();
            using var _ = fixture;

            //Name is NOT NULL, so the database rejects the whole save.
            vendor.Name = null!;
            await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Context.SaveChangesAsync());

            Assert.Empty(audit.PropertyChanges);
        }

        [Fact]
        public async Task AUnitOfWorkRunTwice_RecordsEachChangeOnce()
        {
            var (fixture, audit, vendor) = Build();
            using var _ = fixture;

            //what a retrying execution strategy does: the first attempt rolls back, the tracker is
            //cleared, and the same work runs again in a new transaction.
            await using (var first = await fixture.Context.Database.BeginTransactionAsync())
            {
                vendor.Name = "Cerner";
                await fixture.Context.SaveChangesAsync();
                await first.RollbackAsync();
            }

            var reloaded = await Reload(fixture, vendor.Id);
            await using (var second = await fixture.Context.Database.BeginTransactionAsync())
            {
                reloaded.Name = "Cerner";
                await fixture.Context.SaveChangesAsync();
                await second.CommitAsync();
            }

            var change = Assert.Single(audit.PropertyChanges);
            Assert.Equal("Epic", change.InitialPropertyValue);
            Assert.Equal("Cerner", change.NewPropertyValue);
        }

        [Fact]
        public async Task ATransactionAbandonedWithoutACommit_ContributesNothingToTheNextOne()
        {
            var (fixture, audit, vendor) = Build();
            using var _ = fixture;

            //disposed without commit or rollback, as an exception unwinding an await using does.
            await using (await fixture.Context.Database.BeginTransactionAsync())
            {
                vendor.Name = "Cerner";
                await fixture.Context.SaveChangesAsync();
            }

            var reloaded = await Reload(fixture, vendor.Id);
            await using (var second = await fixture.Context.Database.BeginTransactionAsync())
            {
                reloaded.SigningKey = "unrelated";
                reloaded.ClientSecret = "also-unrelated";
                reloaded.Name = "Meditech";
                await fixture.Context.SaveChangesAsync();
                await second.CommitAsync();
            }

            var change = Assert.Single(audit.PropertyChanges, c => c.PropertyName == nameof(TestVendor.Name));
            Assert.Equal("Epic", change.InitialPropertyValue);
            Assert.Equal("Meditech", change.NewPropertyValue);
        }

        [Fact]
        public async Task EverySaveInACommittedTransaction_IsRecorded()
        {
            var (fixture, audit, vendor) = Build();
            using var _ = fixture;

            await using var transaction = await fixture.Context.Database.BeginTransactionAsync();
            vendor.Name = "Cerner";
            await fixture.Context.SaveChangesAsync();
            vendor.Name = "Meditech";
            await fixture.Context.SaveChangesAsync();
            await transaction.CommitAsync();

            Assert.Collection(audit.PropertyChanges,
                first => Assert.Equal(("Epic", "Cerner"), (first.InitialPropertyValue, first.NewPropertyValue)),
                second => Assert.Equal(("Cerner", "Meditech"), (second.InitialPropertyValue, second.NewPropertyValue)));
        }
    }
}
