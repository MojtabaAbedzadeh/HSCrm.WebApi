using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Entities.Entities
{
    public class InventoryTransaction : AuditedEntity, IFiscalYearScoped
    {
        public int FiscalYearId { get; set; }
        public FiscalYear FiscalYear { get; set; } = default!;

        public int WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; }

        public long Id { get; set; }
        public int TenantId { get; set; }
        public int ProductId { get; set; }
        public decimal QuantityChange { get; set; }

        /// <summary>
        /// فی بهای تمام‌شده هر واحد در این تراکنش (در خرید: فی خالص خرید؛ در فروش: فی بهای تمام‌شده COGS)
        /// </summary>
        public decimal UnitCost { get; set; }

        /// <summary>
        /// ارزش ریالی کل تراکنش انبار (Quantity * UnitCost)
        /// </summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// مقدار باقی‌مانده از این لایه ورود (پارت خرید) جهت مصرف در الگوریتم FIFO
        /// </summary>
        public decimal RemainingFifoQuantity { get; set; }

        public InventoryTransactionType TransactionType { get; set; }
        public long ReferenceId { get; set; }
        public string Description { get; set; }
        public InventoryReferenceType ReferenceType { get; set; }

        // Navigation
        public Product Product { get; set; }
        public Tenant Tenant { get; set; }
    }
}
