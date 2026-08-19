using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Entities.Entities
{
    public class SalesInvoice : TenantScopedEntity, IFiscalYearScoped
    {
        public required string Number { get; set; }

        public int WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; } = default!;

        public string SellerUserId { get; set; }
        public ApplicationUser User { get; set; }

        public int ProjectId { get; set; }
        public Project Project { get; set; } = default!;

        public DateTime IssueDate { get; set; }
        public InvoiceStatus Status { get; set; }

        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public int FiscalYearId { get; set; }
        public FiscalYear FiscalYear { get; set; } = default!;
        public string Description { get; set; }

        public ICollection<SalesInvoiceItem> Items { get; set; } = new List<SalesInvoiceItem>();
    }

}
