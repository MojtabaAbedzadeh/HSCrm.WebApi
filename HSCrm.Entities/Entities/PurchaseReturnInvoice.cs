using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Entities.Entities
{
    /// <summary>
    /// فاکتور برگشت از خرید (Purchase Return Invoice)
    /// </summary>
    public class PurchaseReturnInvoice : TenantScopedEntity, IFiscalYearScoped
    {
        public required string Number { get; set; }

        /// <summary>
        /// شناسه فاکتور خرید مرجع (اختیاری - در صورت برگشت از یک فاکتور خرید مشخص)
        /// </summary>
        public int? PurchaseInvoiceId { get; set; }
        public PurchaseInvoice? PurchaseInvoice { get; set; }

        public int WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; } = default!;

        public string PurchaserUserId { get; set; } = default!;
        public ApplicationUser User { get; set; } = default!;

        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; } = default!;

        public int FiscalYearId { get; set; }
        public FiscalYear FiscalYear { get; set; } = default!;

        public DateTime IssueDate { get; set; }
        public InvoiceStatus Status { get; set; }

        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }

        public string? Description { get; set; }

        public ICollection<PurchaseReturnInvoiceItem> Items { get; set; } = new List<PurchaseReturnInvoiceItem>();
    }
}
