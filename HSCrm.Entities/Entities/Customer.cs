namespace HSCrm.Entities.Entities
{
    public class Customer : TenantScopedEntity
    {
        public required string Name { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
    }
}
