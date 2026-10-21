namespace HSCrm.Entities.Entities
{
    /// <summary>
    /// اقلام فاکتور برگشت از فروش
    /// </summary>
    public class SalesReturnInvoiceItem : TenantScopedEntity
    {
        public int SalesReturnInvoiceId { get; set; }
        public SalesReturnInvoice SalesReturnInvoice { get; set; } = default!;

        public int ProductId { get; set; }
        public Product Product { get; set; } = default!;

        public decimal Quantity { get; set; }
        public decimal UnitPriceReturn { get; set; }
        public decimal? Discount { get; set; }
        public decimal? Tax { get; set; }
        public decimal LineTotal { get; set; }

        /// <summary>
        /// نرخ بهای تمام‌شده هر واحد کالای برگشتی به انبار (برای معکوس کردن COGS)
        /// </summary>
        public decimal UnitCost { get; set; }

        /// <summary>
        /// کل بهای تمام‌شده برگشتی این ردیف (Quantity * UnitCost)
        /// </summary>
        public decimal TotalCost { get; set; }
    }
}
