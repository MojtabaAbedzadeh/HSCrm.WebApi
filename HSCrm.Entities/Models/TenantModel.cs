using static HSCrm.Common.PublicTools.Enums;

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

    public class InvoicePatternUpdateModel
    {
        public string? SalesInvoicePattern { get; set; }
        public string? PurchaseInvoicePattern { get; set; }
        public string? SalesReturnPattern { get; set; }
        public string? PurchaseReturnPattern { get; set; }
    }

    public class CompanySettingsDto
    {
        public int TenantId { get; set; }
        public string TenantTitle { get; set; } = string.Empty;
        public string TenantCode { get; set; } = string.Empty;
        public string? Currency { get; set; } = "IRR";
        public string? TaxId { get; set; }
        public string? CompanyPhone { get; set; }
        public string? CompanyEmail { get; set; }
        public string? Address { get; set; }
        public string? LogoUrl { get; set; }
        public string InvoiceNumberingMode { get; set; } = "Hybrid";
        public string SalesInvoicePattern { get; set; } = "INV-{YEAR}-{SEQ:4}";
        public string PurchaseInvoicePattern { get; set; } = "PINV-{YEAR}-{SEQ:4}";
        public string SalesReturnPattern { get; set; } = "SRI-{YEAR}-{SEQ:4}";
        public string PurchaseReturnPattern { get; set; } = "PRI-{YEAR}-{SEQ:4}";
        public string? SalesInvoicePreview { get; set; }
        public string? PurchaseInvoicePreview { get; set; }
        public string? SalesReturnPreview { get; set; }
        public string? PurchaseReturnPreview { get; set; }
        public decimal DefaultVatPercent { get; set; }
        public string? InvoiceFooterText { get; set; }
        public InventoryCostingMethod DefaultCostingMethod { get; set; } = InventoryCostingMethod.WeightedAverage;
        public DateTime? DocumentLockDate { get; set; }
        public string? DocumentLockDateShamsi { get; set; }
    }

    public class CompanySettingsUpdateModel
    {
        public string TenantTitle { get; set; } = string.Empty;
        public string? Currency { get; set; }
        public string? TaxId { get; set; }
        public string? CompanyPhone { get; set; }
        public string? CompanyEmail { get; set; }
        public string? Address { get; set; }
        public string? LogoUrl { get; set; }
        public string? InvoiceNumberingMode { get; set; }
        public string? SalesInvoicePattern { get; set; }
        public string? PurchaseInvoicePattern { get; set; }
        public string? SalesReturnPattern { get; set; }
        public string? PurchaseReturnPattern { get; set; }
        public decimal DefaultVatPercent { get; set; }
        public string? InvoiceFooterText { get; set; }
        public InventoryCostingMethod DefaultCostingMethod { get; set; } = InventoryCostingMethod.WeightedAverage;
        public string? DocumentLockDate { get; set; }
    }

    public class OnboardingStepDto
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public int CurrentCount { get; set; }
        public string ActionUrl { get; set; } = string.Empty;
    }

    public class OnboardingStatusDto
    {
        public int TotalSteps { get; set; }
        public int CompletedSteps { get; set; }
        public int CompletionPercent { get; set; }
        public bool IsCompleted { get; set; }
        public List<OnboardingStepDto> Steps { get; set; } = new();
    }
}
