using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using LantanaGroup.Link.Submission.Domain.Entities;

namespace LantanaGroup.Link.Submission.Persistence.Interceptors
{
    public class UpdateTenantSubmissionConfigEntityInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            DbContext? context = eventData.Context;
            if (context is null)
            {
                return base.SavingChangesAsync(eventData, result, cancellationToken);
            }

            var entries = context.ChangeTracker.Entries<TenantSubmissionConfigEntity>();

            foreach (var entry in entries)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Property(p => p.CreateDate).CurrentValue = DateTime.UtcNow;
                        break;
                    case EntityState.Modified:
                        entry.Property(p => p.ModifyDate).CurrentValue = DateTime.UtcNow;
                        break;
                }
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
