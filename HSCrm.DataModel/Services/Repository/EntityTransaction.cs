using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HSCrm.DataModel.Services.Repository
{
    public class EntityTransaction : IEntityTransaction
    {
        private readonly IDbContextTransaction? _transaction;

        public EntityTransaction(DbContext context)
        {
            if (context.Database.CurrentTransaction == null &&
                context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
            {
                _transaction = context.Database.BeginTransaction();
            }
        }

        private EntityTransaction(IDbContextTransaction? tx) => _transaction = tx;

        public static async Task<EntityTransaction> StartAsync(DbContext context, CancellationToken ct = default)
        {
            if (context.Database.CurrentTransaction != null ||
                context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
            {
                return new EntityTransaction((IDbContextTransaction?)null);
            }

            var tx = await context.Database.BeginTransactionAsync(ct);
            return new EntityTransaction(tx);
        }

        public static async Task<EntityTransaction> CreateAsync(ApplicationDbContext context, CancellationToken ct = default)
        {
            return await StartAsync(context, ct);
        }

        public void Commit() => _transaction?.Commit();
        public void Rollback() => _transaction?.Rollback();

        public Task CommitAsync(CancellationToken ct = default) =>
            _transaction != null ? _transaction.CommitAsync(ct) : Task.CompletedTask;

        public Task RollbackAsync(CancellationToken ct = default) =>
            _transaction != null ? _transaction.RollbackAsync(ct) : Task.CompletedTask;

        public void Dispose() => _transaction?.Dispose();

        public async ValueTask DisposeAsync()
        {
            if (_transaction != null)
                await _transaction.DisposeAsync();
        }
    }
}
