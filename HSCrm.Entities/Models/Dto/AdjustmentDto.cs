using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Entities.Models.Dto
{
    public class AdjustmentDto
    {
        public int ProductId { get; set; }
        public int WarehouseId { get; set; }
        public decimal QuantityChange { get; set; }
        public string? Description { get; set; }
        public long ReferenceId { get; set; }
        public int TenantId { get; set; }
        public int FiscalYearId { get; set; }
        public InventoryReferenceType ReferenceType { get; set; }
    }
}