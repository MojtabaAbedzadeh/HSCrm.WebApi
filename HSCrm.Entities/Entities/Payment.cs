namespace HSCrm.Entities.Entities
{
    public class Payment : TenantScopedEntity
    {
        public int FiscalYearId { get; set; }
        public FiscalYear FiscalYear { get; set; } = default!;
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
    }
}
