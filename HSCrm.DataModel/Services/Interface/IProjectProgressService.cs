using HSCrm.Entities.Models;

namespace HSCrm.DataModel.Services.Interface
{
    public interface IProjectProgressService
    {
        Task<List<ProjectMilestoneDto>> GetMilestonesAsync(int tenantId, string actorUserId, int projectId, CancellationToken ct = default);
        Task<ProjectMilestoneDto> CreateMilestoneAsync(int tenantId, string actorUserId, ProjectMilestoneCreateModel model, CancellationToken ct = default);
        Task<ProjectMilestoneDto> UpdateMilestoneAsync(int tenantId, string actorUserId, ProjectMilestoneUpdateModel model, CancellationToken ct = default);
        Task<bool> DeleteMilestoneAsync(int tenantId, string actorUserId, int milestoneId, CancellationToken ct = default);

        Task<List<ProjectProgressLogDto>> GetTimelineAsync(int tenantId, string actorUserId, int projectId, int? fiscalYearId = null, CancellationToken ct = default);
        Task<List<ProjectAttachmentDto>> GetGalleryAsync(int tenantId, string actorUserId, int projectId, CancellationToken ct = default);
        Task<ProjectProgressLogDto> AddProgressLogAsync(int tenantId, int fiscalYearId, string actorUserId, ProjectProgressLogCreateInput model, string webRootPath, CancellationToken ct = default);
        Task<bool> DeleteProgressLogAsync(int tenantId, string actorUserId, int logId, CancellationToken ct = default);

        Task<ProjectProfitAndLossDto> GetProjectProfitAndLossAsync(int tenantId, string actorUserId, int projectId, int? fiscalYearId = null, CancellationToken ct = default);
    }
}
