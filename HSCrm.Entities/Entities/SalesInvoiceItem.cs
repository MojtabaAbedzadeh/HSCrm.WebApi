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
        public decimal? Tax { get; set; }
        public decimal LineTotal { get; set; }

        /// <summary>
        /// بهای تمام‌شده هر واحد کالای فروش‌رفته در لحظه خروج از انبار (COGS Unit Cost)
        /// </summary>
        public decimal UnitCost { get; set; }

        /// <summary>
        /// بهای تمام‌شده کل این ردیف فروش (Quantity * UnitCost)
        /// </summary>
        public decimal TotalCost { get; set; }
    }
}
