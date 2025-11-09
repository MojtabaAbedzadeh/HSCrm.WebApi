namespace HSCrm.Entities.Entities
{
    public class SalesInvoice : TenantScopedEntity
    {
        public required string Number { get; set; }
        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = default!;
        public int ProjectId { get; set; }
        public Project Project { get; set; } = default!;
        public DateTime IssueDate { get; set; }
        public string Status { get; set; } = "Draft";
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public string? Notes { get; set; }

        public ICollection<SalesInvoiceItem> Items { get; set; } = new List<SalesInvoiceItem>();
    }
}
