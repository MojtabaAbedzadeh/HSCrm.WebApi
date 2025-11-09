namespace HSCrm.Entities.Entities
{
    public class TenantScopedEntity : AuditedEntity
    {
        public required int TenantId { get; set; }
        public Tenant Tenant { get; set; } = default!;
    }
}
