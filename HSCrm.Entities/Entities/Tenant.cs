using static HSCrm.Common.PublicTools.Enums;

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

        // الگوهای سفارشی‌سازی شماره‌گذاری خودکار فاکتورها
        public string SalesInvoicePattern { get; set; } = "INV-{YEAR}-{SEQ:4}";
        public string PurchaseInvoicePattern { get; set; } = "PINV-{YEAR}-{SEQ:4}";
        public string SalesReturnPattern { get; set; } = "SRI-{YEAR}-{SEQ:4}";
        public string PurchaseReturnPattern { get; set; } = "PRI-{YEAR}-{SEQ:4}";

        // تنظیمات جامع شرکت
        public string InvoiceNumberingMode { get; set; } = "Hybrid"; // Auto, Manual, Hybrid
        public decimal DefaultVatPercent { get; set; } = 0m;
        public string? InvoiceFooterText { get; set; }
        public InventoryCostingMethod DefaultCostingMethod { get; set; } = InventoryCostingMethod.WeightedAverage;
        public DateTime? DocumentLockDate { get; set; }
    }
}
