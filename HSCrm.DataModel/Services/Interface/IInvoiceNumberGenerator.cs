using System;
using System.Threading.Tasks;

namespace HSCrm.DataModel.Services.Interface
{
    public interface IInvoiceNumberGenerator
    {
        Task<string> GenerateNextSalesInvoiceNumberAsync(int tenantId, int? fiscalYearId, DateTime? issueDate = null);
        Task<string> GenerateNextPurchaseInvoiceNumberAsync(int tenantId, int? fiscalYearId, DateTime? issueDate = null);
        Task<string> GenerateNextSalesReturnNumberAsync(int tenantId, int? fiscalYearId, DateTime? issueDate = null);
        Task<string> GenerateNextPurchaseReturnNumberAsync(int tenantId, int? fiscalYearId, DateTime? issueDate = null);
        string FormatNumber(string pattern, string defaultPrefix, int sequenceNumber, DateTime date, string? tenantCode = null);
    }
}
