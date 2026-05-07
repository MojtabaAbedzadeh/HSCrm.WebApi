namespace HSCrm.Entities.Entities
{
    public class PurchaseInvoiceItem : TenantScopedEntity
    {
        public int PurchaseInvoiceId { get; set; }
        public PurchaseInvoice PurchaseInvoice { get; set; } = default!;
        public int ProductId { get; set; }
        public Product Product { get; set; } = default!;
        public decimal Quantity { get; set; }
        public decimal UnitPricePurchase { get; set; }
        public decimal? Discount { get; set; }
        public decimal? Tax { get; set; }
        public decimal LineTotal { get; set; } 
    }
}