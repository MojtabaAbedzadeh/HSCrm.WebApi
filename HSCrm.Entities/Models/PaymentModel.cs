using HSCrm.Entities.Entities;

namespace HSCrm.Entities.Models
{
    public class PaymentCreateModel
    {
        public int? PurchaseInvoiceId { get; set; }
        public PurchaseInvoice? PurchaseInvoice { get; set; }
        public int? ExpenseId { get; set; }
        public Expense? Expense { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; } = "Cash";
        public string? ReferenceNo { get; set; }
        public required string UserId { get; set; }
        public int TenantId { get; set; }
        public Tenant Tenant { get; set; }
    }
    public class PaymentEditModel
    {
        public int PaymentId { get; set; }
        public int? PurchaseInvoiceId { get; set; }
        public PurchaseInvoice? PurchaseInvoice { get; set; }
        public int? ExpenseId { get; set; }
        public Expense? Expense { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; } = "Cash";
        public string? ReferenceNo { get; set; }
        public string Description { get; set; }
        public required string UserId { get; set; }
        public int TenantId { get; set; }
        public Tenant Tenant { get; set; }
    }
    public class PaymentModel
    {
        public int PaymentId { get; set; }
        public int FiscalYearId { get; set; }
        public int? PurchaseInvoiceId { get; set; }
        public int? ExpenseId { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; } = "Cash";
        public string? ReferenceNo { get; set; }
        public string? Description { get; set; }
    }
    public class PaymentDto
    {
        public int PaymentId { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; }
        public string? ReferenceNo { get; set; }
        
        public int? PurchaseInvoiceId { get; set; }
        public int? ExpenseId { get; set; }

        public decimal? InvoiceTotal { get; set; }
        public decimal? PaidAmount { get; set; }
        public decimal? RemainingAmount { get; set; }
    }
    public class PaymentListDto
    {
        public int PaymentId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentDateShamsi { get; set; }
        public string Method { get; set; }
        public string ReferenceNo { get; set; }
        public string RelatedTargetTitle { get; set; } // عنوان فاکتور یا هزینه مرتبط
    }
    public class SettlementStatusDto
    {
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount => TotalAmount - PaidAmount;
        public bool IsSettled => RemainingAmount <= 0;
    }
}
