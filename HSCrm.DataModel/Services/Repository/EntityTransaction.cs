using HSCrm.DataModel.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HSCrm.DataModel.Services.Repository
{
    public class EntityTransaction : IEntityTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public EntityTransaction(DbContext context)
        {
            _transaction = context.Database.BeginTransaction();
        }

        private EntityTransaction(IDbContextTransaction tx) => _transaction = tx;

        public static async Task<EntityTransaction> StartAsync(DbContext context, CancellationToken ct = default)
        {
            var tx = await context.Database.BeginTransactionAsync(ct);
            return new EntityTransaction(tx);
        }

        public void Commit() => _transaction.Commit();
        public void Rollback() => _transaction.Rollback();

        public Task CommitAsync(CancellationToken ct = default) => _transaction.CommitAsync(ct);
        public Task RollbackAsync(CancellationToken ct = default) => _transaction.RollbackAsync(ct);

        public void Dispose() => _transaction.Dispose();
    }
}
