namespace HSCrm.Entities.Entities;

public class Receipt : TenantScopedEntity, IFiscalYearScoped
{
    public int FiscalYearId { get; set; }
    public FiscalYear FiscalYear { get; set; } = default!;

    public int? SalesInvoiceId { get; set; }
    public SalesInvoice? SalesInvoice { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int? BankAccountId { get; set; }
    public BankAccount? BankAccount { get; set; }

    public DateTime ReceiptDate { get; set; }
    public decimal Amount { get; set; }

    public string Method { get; set; } = "Cash";
    public string? ReferenceNo { get; set; }
    public string? Description { get; set; }
}