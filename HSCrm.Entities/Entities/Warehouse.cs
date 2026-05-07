namespace HSCrm.Entities.Entities
{
    public class Warehouse : TenantScopedEntity
    {
        public required string Title { get; set; }
        public string? Address { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
