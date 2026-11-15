using HSCrm.Entities.Models;

namespace HSCrm.DataModel.Services.Interface
{
    public interface IAuditTrailService
    {
        Task<AuditTrailQueryResultDto> GetAuditLogsAsync(
            int tenantId,
            string? entityType = null,
            string? userId = null,
            string? search = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            bool includeDeleted = true,
            int page = 1,
            int pageSize = 50,
            CancellationToken ct = default);

        Task<AuditTrailItemDto?> GetEntityHistoryAsync(
            int tenantId,
            string entityType,
            int entityId,
            CancellationToken ct = default);
    }
}
