using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Entities.Models
{
    #region Sales Return Invoice Models (مدل‌های فاکتور برگشت از فروش)

    public class SalesReturnInvoiceCreateModel
    {
        public int? SalesInvoiceId { get; set; }
        public int ProjectId { get; set; }
        public int WarehouseId { get; set; }
        public int TenantId { get; set; }
        public required string InvoiceNumber { get; set; }
        public InvoiceStatus Status { get; set; } = InvoiceStatus.Final;
        public decimal InvoiceDiscount { get; set; }
        public decimal InvoiceTax { get; set; }
        public required string IssueDate { get; set; } // تاریخ شمسی مثلا 1403/08/10
        public required string SellerUserId { get; set; }
        public string? Description { get; set; }
        public List<SalesReturnInvoiceItemCreateModel> Items { get; set; } = new();
    }

    public class SalesReturnInvoiceItemCreateModel
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public decimal? UnitCost { get; set; }
    }

    public class SalesReturnInvoiceModel
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public int? SalesInvoiceId { get; set; }
        public string? OriginalSalesInvoiceNumber { get; set; }
        public int ProjectId { get; set; }
        public string? ProjectTitle { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public int WarehouseId { get; set; }
        public string? WarehouseTitle { get; set; }
        public DateTime IssueDate { get; set; }
        public InvoiceStatus Status { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public decimal TotalReversedCogs { get; set; }
        public string? Description { get; set; }
    }

    #endregion

    #region Purchase Return Invoice Models (مدل‌های فاکتور برگشت از خرید)

    public class PurchaseReturnInvoiceCreateModel
    {
        public int? PurchaseInvoiceId { get; set; }
        public int SupplierId { get; set; }
        public int WarehouseId { get; set; }
        public int TenantId { get; set; }
        public required string InvoiceNumber { get; set; }
        public InvoiceStatus Status { get; set; } = InvoiceStatus.Final;
        public decimal InvoiceDiscount { get; set; }
        public decimal InvoiceTax { get; set; }
        public required string IssueDate { get; set; } // تاریخ شمسی
        public required string PurchaserUserId { get; set; }
        public string? Description { get; set; }
        public List<PurchaseReturnInvoiceItemCreateModel> Items { get; set; } = new();
    }

    public class PurchaseReturnInvoiceItemCreateModel
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
    }

    public class PurchaseReturnInvoiceModel
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public int? PurchaseInvoiceId { get; set; }
        public string? OriginalPurchaseInvoiceNumber { get; set; }
        public int SupplierId { get; set; }
        public string? SupplierFullName { get; set; }
        public int WarehouseId { get; set; }
        public string? WarehouseTitle { get; set; }
        public DateTime IssueDate { get; set; }
        public InvoiceStatus Status { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public string? Description { get; set; }
    }

    #endregion
}
