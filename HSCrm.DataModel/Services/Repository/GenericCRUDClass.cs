using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
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
        public virtual void Create(TEntity entity) => _table.Add(entity);
        public virtual Task CreateAsync(TEntity entity, CancellationToken ct = default) => _table.AddAsync(entity, ct).AsTask();
        public virtual void CreateRange(IEnumerable<TEntity> entities)
        {
            if (entities == null || !entities.Any()) return;
            _table.AddRange(entities);
        }
        public virtual void Update(TEntity entity, CancellationToken ct = default)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var detachedEntry = _context.Entry(entity);
            var key = detachedEntry.Metadata.FindPrimaryKey()
                ?? throw new InvalidOperationException($"Entity type {typeof(TEntity).Name} has no primary key.");
            var keyValues = key.Properties.Select(property => detachedEntry.Property(property.Name).CurrentValue).ToArray();
            var existing = ByPrimaryKey(keyValues!).FirstOrDefault();
            if (existing == null)
                throw new KeyNotFoundException("رکورد مورد نظر در محدوده دسترسی جاری پیدا نشد.");

            var existingEntry = _context.Entry(existing);
            var protectedValues = new Dictionary<string, object?>();
            foreach (var propertyName in key.Properties.Select(property => property.Name)
                         .Concat(new[] { "TenantId", "CreatedAt", "CreatedById" }))
            {
                if (existingEntry.Metadata.FindProperty(propertyName) != null)
                    protectedValues[propertyName] = existingEntry.Property(propertyName).CurrentValue;
            }

            existingEntry.CurrentValues.SetValues(entity);
            foreach (var pair in protectedValues)
                existingEntry.Property(pair.Key).CurrentValue = pair.Value;
        }
        public virtual IQueryable<TEntity> Get(Expression<Func<TEntity, bool>>? predicate = null, bool asNoTracking = true, params Expression<Func<TEntity, object>>[] includes)
        {
            IQueryable<TEntity> q = _table;
            if (asNoTracking) q = q.AsNoTracking();
            if (predicate != null) q = q.Where(predicate);
            if (includes != null)
                foreach (var inc in includes) q = q.Include(inc);
            return q;
        }
        public virtual Task<List<TEntity>> GetAsync(Expression<Func<TEntity, bool>>? predicate = null, bool asNoTracking = true, CancellationToken ct = default, params Expression<Func<TEntity, object>>[] includes)
        {
            IQueryable<TEntity> q = _table;
            if (asNoTracking) q = q.AsNoTracking();
            if (predicate != null) q = q.Where(predicate);
            if (includes != null)
                foreach (var inc in includes) q = q.Include(inc);
            return q.ToListAsync(ct);
        }

        // Find/FindAsync bypass EF Core global query filters when an entity is already tracked.
        // Build a key predicate instead so Tenant/FiscalYear filters always apply.
        private IQueryable<TEntity> ByPrimaryKey(object id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            var entityType = _context.Model.FindEntityType(typeof(TEntity))
                ?? throw new InvalidOperationException($"Entity type {typeof(TEntity).Name} is not part of the EF model.");
            var key = entityType.FindPrimaryKey()
                ?? throw new InvalidOperationException($"Entity type {typeof(TEntity).Name} has no primary key.");
            var values = id is object[] keyValues ? keyValues : new[] { id };
            if (values.Length != key.Properties.Count)
                throw new ArgumentException($"Expected {key.Properties.Count} key value(s).", nameof(id));

            var parameter = Expression.Parameter(typeof(TEntity), "entity");
            Expression? predicate = null;
            for (var index = 0; index < key.Properties.Count; index++)
            {
                var property = key.Properties[index];
                Expression member;
                if (property.PropertyInfo != null)
                    member = Expression.Property(parameter, property.PropertyInfo);
                else
                {
                    var efProperty = typeof(EF).GetMethod(nameof(EF.Property))!.MakeGenericMethod(property.ClrType);
                    member = Expression.Call(efProperty, parameter, Expression.Constant(property.Name));
                }

                var targetType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                var rawValue = values[index];
                var converted = targetType.IsInstanceOfType(rawValue)
                    ? rawValue
                    : targetType == typeof(Guid)
                        ? Guid.Parse(Convert.ToString(rawValue)!)
                        : Convert.ChangeType(rawValue, targetType, System.Globalization.CultureInfo.InvariantCulture);
                Expression constant = Expression.Constant(converted, targetType);
                if (targetType != property.ClrType)
                    constant = Expression.Convert(constant, property.ClrType);

                var equals = Expression.Equal(member, constant);
                predicate = predicate == null ? equals : Expression.AndAlso(predicate, equals);
            }

            return _table.Where(Expression.Lambda<Func<TEntity, bool>>(predicate!, parameter));
        }

        public virtual TEntity? GetById(object id) => ByPrimaryKey(id).FirstOrDefault();
        public virtual Task<TEntity?> GetByIdAsync(object id, CancellationToken ct = default) => ByPrimaryKey(id).FirstOrDefaultAsync(ct);
        public virtual void Delete(TEntity entity, CancellationToken ct = default)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var detachedEntry = _context.Entry(entity);
            var key = detachedEntry.Metadata.FindPrimaryKey()
                ?? throw new InvalidOperationException($"Entity type {typeof(TEntity).Name} has no primary key.");
            var keyValues = key.Properties.Select(property => detachedEntry.Property(property.Name).CurrentValue).ToArray();

            // Always resolve the persisted row through the normal query pipeline so
            // global tenant/fiscal-year filters apply even for detached caller input.
            var existing = ByPrimaryKey(keyValues!).FirstOrDefault();
            if (existing == null)
                throw new KeyNotFoundException("رکورد مورد نظر در محدوده دسترسی جاری پیدا نشد.");

            _table.Remove(existing);
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
        public virtual void RemoveRange(IEnumerable<TEntity> entities)
        {
            if (entities == null || !entities.Any()) return;
            _table.RemoveRange(entities);
        }
        public virtual IQueryable<TEntity> Query(bool asNoTracking = true) => asNoTracking ? _table.AsNoTracking() : _table;
    }
}