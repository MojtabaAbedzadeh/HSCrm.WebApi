using HSCrm.Entities.Models;

namespace HSCrm.DataModel.Services.Interface
{
    public interface ICustomerPortalService
    {
        Task<CustomerPortalDashboardDto> GetDashboardAsync(int tenantId, string userId, CancellationToken ct = default);
        Task<List<CustomerPortalProjectSummaryDto>> GetProjectsAsync(int tenantId, string userId, CancellationToken ct = default);
        Task<CustomerPortalProjectDetailDto> GetProjectDetailAsync(int tenantId, string userId, int projectId, CancellationToken ct = default);
        Task<List<CustomerPortalInvoiceDto>> GetInvoicesAsync(int tenantId, string userId, CancellationToken ct = default);
        Task<CustomerPortalInvoiceDto> GetInvoiceByIdAsync(int tenantId, string userId, int invoiceId, CancellationToken ct = default);
        Task<CustomerPortalStatementDto> GetStatementAsync(int tenantId, string userId, DateTime? fromDate = null, DateTime? toDate = null, int? fiscalYearId = null, CancellationToken ct = default);
        Task<CustomerPortalProfileDto> GetProfileAsync(int tenantId, string userId, CancellationToken ct = default);
        Task<CustomerPortalProfileDto> UpdateProfileAsync(int tenantId, string userId, CustomerPortalProfileUpdateModel model, CancellationToken ct = default);
        Task<(bool Succeeded, string Message)> ChangePasswordAsync(int tenantId, string userId, CustomerPortalChangePasswordModel model, CancellationToken ct = default);
    }
}
