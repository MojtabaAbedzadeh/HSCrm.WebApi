using HSCrm.Entities.Entities;

namespace HSCrm.DataModel.Services.Interface
{
    public interface IProjectAccessService
    {
        Task<bool> HasFullProjectAccessAsync(string userId, CancellationToken ct = default);
        Task<List<int>> GetAccessibleProjectIdsAsync(int tenantId, string userId, CancellationToken ct = default);
        Task<bool> CanAccessProjectAsync(int tenantId, string userId, int projectId, CancellationToken ct = default);
        Task<List<ProjectMember>> GetUserMembershipsAsync(int tenantId, string userId, CancellationToken ct = default);
    }
}
