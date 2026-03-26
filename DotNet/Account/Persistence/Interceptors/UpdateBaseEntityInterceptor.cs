using LantanaGroup.Link.Account.Application.Interfaces.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LantanaGroup.Link.Account.Persistence.Interceptors
{
    public class UpdateBaseEntityInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                       DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            DbContext? context = eventData.Context;
            if (context is null)
            {
                return base.SavingChangesAsync(eventData, result, cancellationToken);
            }

            var entries = context.ChangeTracker.Entries<IBaseEntity>();

            foreach (var entry in entries)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Property(p => p.CreatedOn).CurrentValue = DateTime.UtcNow;
                        break;
                    case EntityState.Modified:
                        entry.Property(p => p.LastModifiedOn).CurrentValue = DateTime.UtcNow;
                        break;                   
                }
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
