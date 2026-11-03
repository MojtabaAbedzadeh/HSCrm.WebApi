using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Entities.Models.Dto
{
    public class AdjustmentDto
    {
        public int ProductId { get; set; }
        public int WarehouseId { get; set; }
        public decimal ActualQuantity { get; set; }
        public string? Description { get; set; }
    }
}