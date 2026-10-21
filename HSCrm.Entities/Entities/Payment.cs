namespace HSCrm.Entities.Entities
{
    public class Payment : TenantScopedEntity, IFiscalYearScoped
    {
        public int FiscalYearId { get; set; }
        public FiscalYear FiscalYear { get; set; } = default!;

        public int? PurchaseInvoiceId { get; set; }
        public PurchaseInvoice? PurchaseInvoice { get; set; }

        public int? ExpenseId { get; set; }
        public Expense? Expense { get; set; }

        public int? BankAccountId { get; set; }
        public BankAccount? BankAccount { get; set; }

        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }

        public string Method { get; set; } = "Cash";
        public string? ReferenceNo { get; set; }
        public string? Description { get; set; }
    }
}