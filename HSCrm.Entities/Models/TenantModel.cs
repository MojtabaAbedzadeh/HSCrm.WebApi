namespace HSCrm.Entities.Models
{
    public class TenantCreateModel
    {
        public required string TenantTitle { get; set; }
        public string? CompanyPhone { get; set; }
        public string? CompanyEmail { get; set; }
        public string? Address { get; set; }
        public string? LogoUrl { get; set; }
        public bool IsActive { get; set; }
        public string? Currency { get; set; }
        public DateTime CreateAt { get; set; }
        public required string CreatedBy { get; set; }
        public required string TenantUserName { get; set; }
        public required string TenantPassword { get; set; }
    }

    public class TenantEditModel
    {
        public int TenantId { get; set; }
        public required string TenantTitle { get; set; }
        public string? CompanyPhone { get; set; }
        public string? CompanyEmail { get; set; }
        public string? Address { get; set; }
        public string? LogoUrl { get; set; }
        public bool IsActive { get; set; }
    }
}


    public class InvoicePatternUpdateModel
    {
        public string? SalesInvoicePattern { get; set; }
        public string? PurchaseInvoicePattern { get; set; }
        public string? SalesReturnPattern { get; set; }
        public string? PurchaseReturnPattern { get; set; }
    }
