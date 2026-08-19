namespace HSCrm.DataModel.Services.Interface
{
    public interface IEntityTransaction : IDisposable, IAsyncDisposable
    {
        void Commit();
        void Rollback();
        Task CommitAsync(CancellationToken ct = default);
        Task RollbackAsync(CancellationToken ct = default);
    }
}
