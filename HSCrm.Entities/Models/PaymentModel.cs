using HSCrm.Entities.Entities;

namespace HSCrm.Entities.Models
{
    public class PaymentCreateModel
    {
        public int? SalesInvoiceId { get; set; }
        public SalesInvoice? SalesInvoice { get; set; }
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
        public int? SalesInvoiceId { get; set; }
        public SalesInvoice? SalesInvoice { get; set; }
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
}
