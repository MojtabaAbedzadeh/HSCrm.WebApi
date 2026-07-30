using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Entities.Models.Dto
{
    public class SettlementSummaryDto
    {
        public int? SalesInvoiceId { get; set; }
        public int? PurchaseInvoiceId { get; set; }
        public int? ExpenseId { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public SettlementStatus SettlementStatus { get; set; }

        public List<PaymentItemDto> Payments { get; set; } = new();
    }

    public class PaymentItemDto
    {
        public int PaymentId { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; } = default!;
        public string? ReferenceNo { get; set; }
        public DateTime PaymentDate { get; set; }
    }
}
