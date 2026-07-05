namespace HSCrm.Entities.Entities
{
    public class TenantScopedEntity : AuditedEntity
    {
        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = default!;
    }
}
