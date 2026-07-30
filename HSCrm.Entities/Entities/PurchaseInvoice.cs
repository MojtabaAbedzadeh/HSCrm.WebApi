using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Entities.Entities
{
    public class PurchaseInvoice : TenantScopedEntity, IFiscalYearScoped
    {
        public required string Number { get; set; }

        public int WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; } = default!;

        public string PurchaserUserId { get; set; }
        public ApplicationUser User { get; set; }

        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; } = default!;

        public SalesInvoiceStatus Status { get; set; }

        public DateTime IssueDate { get; set; }

        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public SettlementStatus PayStatus { get; set; } = SettlementStatus.Unpaid;
        public decimal PaidAmount { get; set; }

        public ICollection<PurchaseInvoiceItem> Items { get; set; } = new List<PurchaseInvoiceItem>();
        public int FiscalYearId { get; set; }
        public FiscalYear FiscalYear { get; set; } = default!;
    }
}