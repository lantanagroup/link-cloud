using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace LantanaGroup.Link.Account.Repositories
{
    public class TestDataContext : DbContext
    {
        public virtual DbSet<AccountModel> Accounts { get; set; }
        public virtual DbSet<GroupModel> Groups { get; set; }
        public virtual DbSet<RoleModel> Roles { get; set; }

        public TestDataContext(DbContextOptions options) : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // manually set bridge table names

            modelBuilder.Entity<AccountModel>()
                .HasMany(left => left.Groups)
                .WithMany(right => right.Accounts)
                .UsingEntity(join => join.ToTable("AccountGroups"));

            modelBuilder.Entity<AccountModel>()
                .HasMany(left => left.Roles)
                .WithMany(right => right.Accounts)
                .UsingEntity(join => join.ToTable("AccountRoles"));


            modelBuilder.Entity<GroupModel>()
                .HasMany(left => left.Roles)
                .WithMany(right => right.Groups)
                .UsingEntity(join => join.ToTable("GroupRoles"));


            // Filter query for deleted entities
            modelBuilder.Entity<AccountModel>().HasQueryFilter(a => !a.IsDeleted);
            modelBuilder.Entity<GroupModel>().HasQueryFilter(a => !a.IsDeleted);
            modelBuilder.Entity<RoleModel>().HasQueryFilter(a => !a.IsDeleted);
        }

        public override int SaveChanges()
        {
            PreSaveData();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            PreSaveData();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            PreSaveData();           
            var res = await base.SaveChangesAsync(cancellationToken);          
            return res;
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            PreSaveData();
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }


        /// <summary>
        /// Handles any final entity changes before save is executed.
        /// Ensures auditing fields are correctly populated regardless of any supplied values.
        /// </summary>
        protected void PreSaveData()
        {
            //_logger.LogInformation($"PreSaveData: {ChangeTracker.Entries().Count()}");

            foreach (var changed in ChangeTracker.Entries())
            {

                if (changed.Entity is IBaseEntity entity)
                {
                    switch (changed.State)
                    {
                        // Entity modified -- set LastModifiedOn and ensure original CreatedOn is retained
                        case EntityState.Modified:
                            entity.CreatedOn = changed.OriginalValues.GetValue<DateTime>(nameof(entity.CreatedOn));
                            entity.LastModifiedOn = DateTime.UtcNow;
                            break;
                        // Entity created -- set CreatedOn
                        case EntityState.Added:
                            entity.CreatedOn = DateTime.UtcNow;
                            break;
                    }
                }
            }
        }        

    }
}
