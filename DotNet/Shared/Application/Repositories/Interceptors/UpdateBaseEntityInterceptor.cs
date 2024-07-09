using LantanaGroup.Link.Shared.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LantanaGroup.Link.Shared.Application.Repositories.Interceptors
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

                var entries = context.ChangeTracker.Entries<BaseEntityExtended>();

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
