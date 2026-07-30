using HSCrm.Entities.Models.Dto;

namespace HSCrm.DataModel.Services.Interface
{
    public interface IPaymentSettlementService
    {
        SettlementSummaryDto BuildSalesInvoiceSummary(int salesInvoiceId, decimal totalAmount);
        SettlementSummaryDto BuildPurchaseInvoiceSummary(int purchaseInvoiceId, decimal totalAmount);
        SettlementSummaryDto BuildExpenseSummary(int expenseId, decimal totalAmount);
    }
}