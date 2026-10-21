using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HSCrm.Entities.Entities
{
    public class Inventory : AuditedEntity
    {
        public long Id { get; set; }
        public int TenantId { get; set; }
        public int WarehouseId { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }

        /// <summary>
        /// بهای تمام‌شده هر واحد کالا به روش میانگین موزون متحرک (Moving Weighted Average Cost)
        /// </summary>
        public decimal AverageUnitCost { get; set; }

        /// <summary>
        /// ارزش ریالی کل موجودی این کالا در این انبار
        /// </summary>
        [NotMapped]
        public decimal TotalInventoryValue => Math.Round(Quantity * AverageUnitCost, 2);

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        // Navigation
        public Warehouse Warehouse { get; set; }
        public Product Product { get; set; }
        public Tenant Tenant { get; set; }
    }
}
