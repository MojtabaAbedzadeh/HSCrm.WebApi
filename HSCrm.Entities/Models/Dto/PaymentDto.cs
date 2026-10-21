public class PaymentDto
{
    public int Id { get; set; }

    public decimal Amount { get; set; }

    public DateTime PaymentDate { get; set; }

    public string Method { get; set; } = string.Empty;

    public string? ReferenceNo { get; set; }
    public int? BankAccountId { get; set; }
    public string? BankAccountTitle { get; set; }

    public int? PurchaseInvoiceId { get; set; }

    public int? ExpenseId { get; set; }

    public string Title { get; set; } = string.Empty;
}