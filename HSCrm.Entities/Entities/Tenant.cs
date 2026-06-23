namespace HSCrm.Entities.Entities
{
    public class Tenant : AuditedEntity
    {
        public required string TenantTitle { get; set; }
        public string TenantCode { get; set; } = default!;
        public string? Currency { get; set; } = "IRR";
        public string? TaxId { get; set; }
        public string? CompanyPhone { get; set; }
        public string? CompanyEmail { get; set; }
        public string? Address { get; set; }
        public string? LogoUrl { get; set; }
        public bool IsActive { get; set; }
    }
}