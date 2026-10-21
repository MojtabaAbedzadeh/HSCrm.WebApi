using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Entities.Entities
{
    /// <summary>
    /// فاکتور برگشت از فروش (Sales Return Invoice)
    /// </summary>
    public class SalesReturnInvoice : TenantScopedEntity, IFiscalYearScoped
    {
        public required string Number { get; set; }

        /// <summary>
        /// شناسه فاکتور فروش مرجع (اختیاری - در صورت برگشت از یک فاکتور مشخص)
        /// </summary>
        public int? SalesInvoiceId { get; set; }
        public SalesInvoice? SalesInvoice { get; set; }

        public int WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; } = default!;

        public string SellerUserId { get; set; } = default!;
        public ApplicationUser User { get; set; } = default!;

        public int ProjectId { get; set; }
        public Project Project { get; set; } = default!;

        public int FiscalYearId { get; set; }
        public FiscalYear FiscalYear { get; set; } = default!;

        public DateTime IssueDate { get; set; }
        public InvoiceStatus Status { get; set; }

        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }

        public string? Description { get; set; }

        public ICollection<SalesReturnInvoiceItem> Items { get; set; } = new List<SalesReturnInvoiceItem>();
    }
}
