namespace HSCrm.DataModel.Services.Interface
{
    public interface IEntityTransaction : IDisposable
    {
        void Commit();
        void Rollback();

        // Async
        Task CommitAsync(CancellationToken ct = default);
        Task RollbackAsync(CancellationToken ct = default);
    }
}
