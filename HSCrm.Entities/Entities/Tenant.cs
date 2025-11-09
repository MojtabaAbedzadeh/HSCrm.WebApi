using System.ComponentModel.DataAnnotations;

namespace HSCrm.Entities.Entities
{
    public class Tenant : AuditedEntity
    {
        public required string TenantTitle { get; set; }
        public required string TenantOwner { get; set; }
        public required string TenantOwnerMobile { get; set; }
        public string? Currency { get; set; } = "IRR";
        public string? TaxId { get; set; }
    }
}
