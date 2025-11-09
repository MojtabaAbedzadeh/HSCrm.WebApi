namespace HSCrm.Entities.Entities
{
    public class SalesInvoiceItem : TenantScopedEntity
    {
        public int SalesInvoiceId { get; set; }
        public SalesInvoice SalesInvoice { get; set; } = default!;
        public int ProductId { get; set; }
        public Product Product { get; set; } = default!;
        public decimal Quantity { get; set; }
        public decimal UnitPriceSell { get; set; }  
        public decimal? Discount { get; set; }
        public decimal LineTotal { get; set; }
    }
}
