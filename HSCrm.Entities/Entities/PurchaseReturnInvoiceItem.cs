namespace HSCrm.Entities.Entities
{
    /// <summary>
    /// اقلام فاکتور برگشت از خرید
    /// </summary>
    public class PurchaseReturnInvoiceItem : TenantScopedEntity
    {
        public int PurchaseReturnInvoiceId { get; set; }
        public PurchaseReturnInvoice PurchaseReturnInvoice { get; set; } = default!;

        public int ProductId { get; set; }
        public Product Product { get; set; } = default!;

        public decimal Quantity { get; set; }
        public decimal UnitPriceReturn { get; set; }
        public decimal? Discount { get; set; }
        public decimal? Tax { get; set; }
        public decimal LineTotal { get; set; }
    }
}
