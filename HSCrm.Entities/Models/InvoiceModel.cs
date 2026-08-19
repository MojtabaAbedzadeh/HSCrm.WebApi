using HSCrm.Entities.Entities;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Entities.Models
{
    #region Purchase Invoice Models (مدل‌های فاکتور خرید)

    public class PurchaseInvoiceCreateModel
    {
        public int SupplierId { get; set; }
        public int WarehouseId { get; set; }
        public int TenantId { get; set; }
        public required string InvoiceNumber { get; set; }
        public InvoiceStatus Status { get; set; }
        public decimal InvoiceDiscount { get; set; }
        public decimal InvoiceTax { get; set; }
        public string IssueDate { get; set; }
        public required string PurchaserId { get; set; }
        public List<PurchaseInvoiceItemCreateModel> Items { get; set; } = new();
    }

    public class PurchaseInvoiceItemCreateModel
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
    }

    public class PurchaseInvoiceEditModel
    {
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public int WarehouseId { get; set; }
        public DateTime IssueDate { get; set; }
        public InvoiceStatus Status { get; set; }
        public int TenantId { get; set; }
        public string PurchaserId { get; set; } = string.Empty;
        public decimal InvoiceTax { get; set; }
        public decimal InvoiceDiscount { get; set; }
        public List<PurchaseInvoiceItemEditModel> Items { get; set; } = new();
    }

    public class PurchaseInvoiceItemEditModel
    {
        public int InvoiceId { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public string? ModifiedById { get; set; }
    }

    public class PurchaseInvoiceModel
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; }
        public DateTime IssueDate { get; set; }
        public InvoiceStatus Status { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public string? Notes { get; set; }
        public SettlementStatus PayStatus { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount => Total - PaidAmount;
    }

    #endregion

    #region Sales Invoice Models (مدل‌های فاکتور فروش)

    public class SalesInvoiceCreateModel
    {
        public int ProjectId { get; set; }
        public int WarehouseId { get; set; }
        public int TenantId { get; set; }
        public required string InvoiceNumber { get; set; }
        public InvoiceStatus Status { get; set; } // Proforma / Final
        public decimal InvoiceDiscount { get; set; }
        public decimal InvoiceTax { get; set; }
        public required string IssueDate { get; set; }
        public required string SellerUserId { get; set; }
        public bool IsPaid { get; set; }
        public decimal PaidAmount { get; set; }
        public string? PaymentMethod { get; set; }
        public string? PaymentReferenceNo { get; set; }
        public List<SalesInvoiceItemCreateModel> Items { get; set; } = new();
    }

    public class SalesInvoiceItemCreateModel
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
    }

    public class SalesInvoiceEditModel
    {
        public int InvoiceId { get; set; }
        public DateTime IssueDate { get; set; }
        public InvoiceStatus Status { get; set; } // Proforma / Final / Cancelled
        public int ProjectId { get; set; }
        public int TenantId { get; set; }
        public required string SellerUserId { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public List<SalesInvoiceItemEditModel> Items { get; set; } = new();
    }

    public class SalesInvoiceItemEditModel
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class SalesInvoiceModel
    {
        public int Id { get; set; }
        public required string Number { get; set; }
        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = default!;
        public int? ProjectId { get; set; }
        public Project Project { get; set; }
        public DateTime IssueDate { get; set; }
        public InvoiceStatus Status { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public string? Notes { get; set; }
    }

    #endregion
}