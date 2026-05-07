using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace HSCrm.DataModel.Services.Repository
{
    public class GenericCRUDClass<TEntity> where TEntity : class
    {
        private readonly DbContext _context;
        private readonly DbSet<TEntity> _table;

        public GenericCRUDClass(DbContext context)
        {
            _context = context;
            _table = _context.Set<TEntity>();
        }

        // CREATE
        public virtual void Create(TEntity entity) => _table.Add(entity);
        public virtual Task CreateAsync(TEntity entity, CancellationToken ct = default) => _table.AddAsync(entity, ct).AsTask();
        public virtual void CreateRange(IEnumerable<TEntity> entities)
        {
            if (entities == null || !entities.Any())
                return;

            _table.AddRange(entities);
        }


        // UPDATE
        public virtual void Update(TEntity entity)
        {
            _table.Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
        }

        // READ (sync)
        public virtual IEnumerable<TEntity> Get(
            Expression<Func<TEntity, bool>>? predicate = null,
            bool asNoTracking = true,
            params Expression<Func<TEntity, object>>[] includes)
        {
            IQueryable<TEntity> q = _table;
            if (asNoTracking)
                q = q.AsNoTracking();
            if (predicate != null) q = q.Where(predicate);
            if (includes != null)
                foreach (var inc in includes) q = q.Include(inc);
            return q;
        }

        // READ (async)
        public virtual Task<List<TEntity>> GetAsync(
            Expression<Func<TEntity, bool>>? predicate = null,
            bool asNoTracking = true,
            CancellationToken ct = default,
            params Expression<Func<TEntity, object>>[] includes)
        {
            IQueryable<TEntity> q = _table;
            if (asNoTracking) q = q.AsNoTracking();
            if (predicate != null) q = q.Where(predicate);
            if (includes != null)
                foreach (var inc in includes) q = q.Include(inc);
            return q.ToListAsync(ct);
        }

        public virtual TEntity? GetById(object id) => _table.Find(id);
        public virtual Task<TEntity?> GetByIdAsync(object id, CancellationToken ct = default) => _table.FindAsync(new[] { id }, ct).AsTask();

        // DELETE
        public virtual void Delete(TEntity entity)
        {
            if (_context.Entry(entity).State == EntityState.Detached)
                _table.Attach(entity);
            _table.Remove(entity);
        }

        public virtual void DeleteById(object id)
        {
            var entity = GetById(id);
            if (entity != null) Delete(entity);
        }

        public virtual void DeleteByRange(Expression<Func<TEntity, bool>>? predicate = null)
        {
            IQueryable<TEntity> q = _table;
            if (predicate != null) q = q.Where(predicate);
            _table.RemoveRange(q);
        }
    }
}
