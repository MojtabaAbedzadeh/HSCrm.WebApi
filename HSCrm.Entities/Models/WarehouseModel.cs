namespace HSCrm.Entities.Models
{
    public class WarehouseCreateModel
    {
        public required string Title { get; set; }
        public string? Address { get; set; }
        public bool IsActive { get; set; } = true;
        public required string CreateById { get; set; }
        public int TenantId { get; set; }
    }
    public class WarehouseEditModel
    {
        public int WarehouseId { get; set; }
        public required string Title { get; set; }
        public string? Address { get; set; }
        public bool IsActive { get; set; } = true;
        public required string ModifiedById { get; set; }
    }
}
