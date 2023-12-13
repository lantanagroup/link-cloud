using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Shared.Configs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Account.Repositories
{
    public class BaseRepository<T> where T : BaseEntity
    {
        protected readonly ILogger _logger;
        protected readonly DataContext _dataContext;

        public BaseRepository(ILogger logger, DataContext dataContext)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _dataContext = dataContext;
        }


        public virtual IEnumerable<T> GetAll()
        {
            return _dataContext.Set<T>().ToList();
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync(bool noTracking = false)
        {
            return noTracking ?
                await _dataContext.Set<T>().AsNoTracking().ToListAsync() :
                await _dataContext.Set<T>().ToListAsync();
        }

        public virtual T Get(Guid id, bool noTracking = false)
        {
            var query = noTracking ? _dataContext.Set<T>().AsNoTracking() : _dataContext.Set<T>();
            return query.FirstOrDefault(a => a.Id == id);
        }

        public virtual async Task<T> GetAsync(Guid id, bool noTracking = false, CancellationToken cancellationToken = default)
        {
            var query = noTracking ? _dataContext.Set<T>().AsNoTracking() : _dataContext.Set<T>();
            return await query.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        }

        public virtual bool Exists(Guid id)
        {
            var res = _dataContext.Set<T>().AsNoTracking().FirstOrDefault(a => a.Id == id);
            if (res != null)
                return true;
            return false;
        }

        public virtual async Task<bool> ExistsAsync(Guid id)
        {
            var res = await _dataContext.Set<T>().AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
            if (res != null)
                return true;
            return false;
        }


        public virtual bool Add(T newEntity)
        {
            try
            {
                _dataContext.Add(newEntity);
                _dataContext.SaveChanges();
            }
            catch (Exception ex)
            {
                _logger.LogError($"AddAsync exception: {ex.Message}");
                throw;
            }

            return true;
        }

        public virtual async Task<bool> AddAsync(T newEntity, CancellationToken cancellationToken = default)
        {
            try
            {
                await _dataContext.AddAsync(newEntity, cancellationToken);
                await _dataContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError($"AddAsync exception: {ex.Message}");
                throw;
            }

            return true;
        }


        public virtual T Update(T updatedEntity)
        {
            var originalEntity = Get(updatedEntity.Id);
            if (originalEntity == null)
            {
                return null;
            }

            _dataContext.Entry(originalEntity).CurrentValues.SetValues(updatedEntity);
            _dataContext.SaveChanges();
            return updatedEntity;
        }

        public virtual async Task<T> UpdateAsync(T updatedEntity, CancellationToken cancellationToken = default)
        {
            var originalEntity = await GetAsync(updatedEntity.Id);
            if (originalEntity == null)
            {
                return null;
            }

            _dataContext.Entry(originalEntity).CurrentValues.SetValues(updatedEntity);
            await _dataContext.SaveChangesAsync(cancellationToken);
            return originalEntity;
        }


        public virtual void Delete(Guid id)
        {
            var entity = _dataContext.Set<T>().FirstOrDefault(a => a.Id == id);
            if (entity == null)
            {
                return;
            }

            entity.IsDeleted = true;

            _dataContext.Set<T>().Update(entity);
            _dataContext.SaveChanges();
        }

        public virtual async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await _dataContext.Set<T>().FirstOrDefaultAsync(a => a.Id == id);
            if (entity == null)
            {
                return;
            }

            entity.IsDeleted = true;

            _dataContext.Set<T>().Update(entity);
            await _dataContext.SaveChangesAsync(cancellationToken);
        }

        public virtual T Restore(Guid id)
        {
            var entity = _dataContext.Set<T>().IgnoreQueryFilters().FirstOrDefault(a => a.Id == id);
            if (entity == null)
            {
                return null;
            }

            entity.IsDeleted = false;

            _dataContext.Set<T>().Update(entity);
            _dataContext.SaveChanges();

            return entity;
        }

        public virtual async Task<T> RestoreAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await _dataContext.Set<T>().IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == id);
            if (entity == null)
            {
                return null;
            }

            entity.IsDeleted = false;

            _dataContext.Set<T>().Update(entity);
            await _dataContext.SaveChangesAsync(cancellationToken);

            return entity;
        }      
    }
}
