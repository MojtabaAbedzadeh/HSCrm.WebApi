using HSCrm.Entities.Entities;

namespace HSCrm.Entities.Models
{
    public class ReceiptCreateModel
    {
        public int? SalesInvoiceId { get; set; }
        public int? CustomerId { get; set; }
        public int? BankAccountId { get; set; }
        public string? BankAccountTitle { get; set; }

        public DateTime ReceiptDate { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; } = "Cash";
        public string? ReferenceNo { get; set; }
        public string? Description { get; set; }

        public string? UserId { get; set; }
        public int TenantId { get; set; }
        public Tenant? Tenant { get; set; }
    }

    public class ReceiptEditModel
    {
        public int ReceiptId { get; set; }

        public int? SalesInvoiceId { get; set; }
        public int? CustomerId { get; set; }
        public int? BankAccountId { get; set; }
        public string? BankAccountTitle { get; set; }

        public DateTime ReceiptDate { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; } = "Cash";
        public string? ReferenceNo { get; set; }
        public string? Description { get; set; }
    }

    public class ReceiptModel
    {
        public int ReceiptId { get; set; }
        public int FiscalYearId { get; set; }
        public int? SalesInvoiceId { get; set; }
        public int? CustomerId { get; set; }
        public int? BankAccountId { get; set; }
        public string? BankAccountTitle { get; set; }
        public string? CustomerName { get; set; }
        public string? SalesInvoiceNumber { get; set; }
        public DateTime ReceiptDate { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; } = "Cash";
        public string? ReferenceNo { get; set; }
        public string? Description { get; set; }
    }
}