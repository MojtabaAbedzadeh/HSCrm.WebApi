namespace HSCrm.Entities.Models.Dto;

// DTO خلاصه وضعیت مالی تامین‌کننده برای داخل مودال
public class SupplierFinancialProfileDto
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;

    public decimal TotalInvoices { get; set; } // جمع کل فاکتورها
    public decimal TotalPayments { get; set; } // جمع کل پرداخت‌ها

    public decimal BalanceDue => TotalInvoices - TotalPayments;

    public List<SupplierInvoiceDto> Invoices { get; set; } = new();
    public List<SupplierPaymentDto> Payments { get; set; } = new();
}

public class SupplierInvoiceDto
{
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Description { get; set; }
}

public class SupplierPaymentDto
{
    public int PaymentId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string Method { get; set; } = string.Empty;
    public string? ReferenceNo { get; set; }
}
