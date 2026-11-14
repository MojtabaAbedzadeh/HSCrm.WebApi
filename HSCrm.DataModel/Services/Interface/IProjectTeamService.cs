using HSCrm.Entities.Models;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.DataModel.Services.Interface
{
    public interface IProjectTeamService
    {
        Task<List<ProjectMemberDto>> GetProjectMembersAsync(int tenantId, int projectId, CancellationToken ct = default);
        Task<Dictionary<int, int>> GetProjectMemberCountsAsync(int tenantId, string actorUserId, CancellationToken ct = default);
        Task<List<AssignableUserDto>> GetAssignableUsersAsync(int tenantId, CancellationToken ct = default);
        Task<ProjectMemberDto> AddOrUpdateMemberAsync(int tenantId, string actorUserId, ProjectMemberUpsertModel model, CancellationToken ct = default);
        Task<bool> RemoveMemberAsync(int tenantId, int memberId, string actorUserId, CancellationToken ct = default);

        Task<List<ProjectWorkLogDto>> GetWorkLogsAsync(
            int tenantId,
            int fiscalYearId,
            string actorUserId,
            int? projectId = null,
            string? targetUserId = null,
            WorkLogStatus? status = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default);

        Task<ProjectWorkLogDto> CreateWorkLogAsync(int tenantId, int fiscalYearId, string actorUserId, ProjectWorkLogCreateModel model, CancellationToken ct = default);
        Task<ProjectWorkLogDto> UpdateWorkLogAsync(int tenantId, int fiscalYearId, string actorUserId, ProjectWorkLogEditModel model, CancellationToken ct = default);
        Task<bool> DeleteWorkLogAsync(int tenantId, int fiscalYearId, string actorUserId, int id, CancellationToken ct = default);
        Task<ProjectWorkLogDto> ReviewWorkLogAsync(int tenantId, int fiscalYearId, string reviewerUserId, ProjectWorkLogReviewModel model, CancellationToken ct = default);
        Task<int> BatchReviewWorkLogsAsync(int tenantId, int fiscalYearId, string reviewerUserId, ProjectWorkLogBatchReviewModel model, CancellationToken ct = default);
        Task<EmployeeDashboardDto> GetEmployeeDashboardAsync(int tenantId, int fiscalYearId, string userId, CancellationToken ct = default);
    }
}
