namespace HSCrm.Entities.Entities
{
    public class PurchaseInvoice : TenantScopedEntity
    {
        public required string Number { get; set; }    
        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; } = default!;
        public int? ProjectId { get; set; }    
        public Project? Project { get; set; }
        public DateTime IssueDate { get; set; }
        public byte PayStatus { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public string? Notes { get; set; }

        public ICollection<PurchaseInvoiceItem> Items { get; set; } = new List<PurchaseInvoiceItem>();
    }
}
