using System.ComponentModel.DataAnnotations;

namespace HSCrm.Entities.Entities
{
    public class Inventory : AuditedEntity
    {
        public long Id { get; set; }
        public int TenantId { get; set; }
        public int WarehouseId { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        [Timestamp]        
        public byte[]? RowVersion { get; set; }
        // Navigation
        public Warehouse Warehouse { get; set; }
        public Product Product { get; set; }
        public Tenant Tenant { get; set; }
    }
}
