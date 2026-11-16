using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Entities.Models
{
    public class CustomerPortalAccountActivationModel
    {
        public int CustomerId { get; set; }
        public required string UserName { get; set; }
        public required string Password { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
    }

    public class CustomerPortalAccountResetPasswordModel
    {
        public int CustomerId { get; set; }
        public required string NewPassword { get; set; }
    }

    public class CustomerPortalStatusDto
    {
        public int CustomerId { get; set; }
        public bool HasPortalAccount { get; set; }
        public string? UserName { get; set; }
        public bool IsActive { get; set; }
    }

    public class CustomerPortalProjectSummaryDto
    {
        public int Id { get; set; }
        public string ProjectTitle { get; set; } = string.Empty;
        public string? ProjectLocation { get; set; }
        public string Status { get; set; } = "Active";
        public string StatusTitle { get; set; } = string.Empty;
        public decimal ProgressPercent { get; set; }
        public int MilestonesCount { get; set; }
        public int CompletedMilestonesCount { get; set; }
        public int ProgressLogsCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedAtShamsi { get; set; } = string.Empty;
    }

    public class CustomerPortalMilestoneDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SortOrder { get; set; }
        public decimal WeightPercent { get; set; }
        public decimal ProgressPercent { get; set; }
        public MilestoneStatus Status { get; set; }
        public string StatusTitle { get; set; } = string.Empty;
        public string? DueDateShamsi { get; set; }
        public string? CompletedAtShamsi { get; set; }
    }

    public class CustomerPortalProgressItemDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string ProjectTitle { get; set; } = string.Empty;
        public int? MilestoneId { get; set; }
        public string? MilestoneTitle { get; set; }
        public DateTime LogDate { get; set; }
        public string LogDateShamsi { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ReportText { get; set; } = string.Empty;
        public decimal ProgressPercent { get; set; }
        public string? WorkshopArea { get; set; }
        public string RecordedByName { get; set; } = string.Empty;
        public List<ProjectAttachmentDto> Attachments { get; set; } = new();
    }

    public class CustomerPortalProjectDetailDto
    {
        public int Id { get; set; }
        public string ProjectTitle { get; set; } = string.Empty;
        public string? ProjectLocation { get; set; }
        public string Status { get; set; } = "Active";
        public string StatusTitle { get; set; } = string.Empty;
        public decimal ProgressPercent { get; set; }
        public string CreatedAtShamsi { get; set; } = string.Empty;
        public List<CustomerPortalMilestoneDto> Milestones { get; set; } = new();
        public List<CustomerPortalProgressItemDto> ProgressLogs { get; set; } = new();
    }

    public class CustomerPortalCompanyPrintInfoDto
    {
        public string CompanyTitle { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string? TaxId { get; set; }
        public string? CompanyPhone { get; set; }
        public string? CompanyEmail { get; set; }
        public string? Address { get; set; }
        public string? InvoiceFooterText { get; set; }
        public string Currency { get; set; } = "IRR";
        public string? InvoicePrintTemplateJson { get; set; }
    }

    public class CustomerPortalInvoiceLineDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductTitle { get; set; } = string.Empty;
        public string? Unit { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
    }

    public class CustomerPortalInvoiceDto
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public int ProjectId { get; set; }
        public string ProjectTitle { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public string? CustomerAddress { get; set; }
        public DateTime IssueDate { get; set; }
        public string IssueDateShamsi { get; set; } = string.Empty;
        public InvoiceStatus Status { get; set; }
        public string StatusTitle { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public string? Description { get; set; }
        public List<CustomerPortalInvoiceLineDto> Items { get; set; } = new();
        public CustomerPortalCompanyPrintInfoDto CompanyInfo { get; set; } = new();
    }

    public class CustomerPortalReceiptDto
    {
        public int Id { get; set; }
        public DateTime ReceiptDate { get; set; }
        public string ReceiptDateShamsi { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Method { get; set; } = "Cash";
        public string MethodTitle { get; set; } = string.Empty;
        public string? ReferenceNo { get; set; }
        public string? BankAccountTitle { get; set; }
        public int? SalesInvoiceId { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? Description { get; set; }
    }

    public class CustomerPortalStatementDto
    {
        public PartyStatementReportDto? Statement { get; set; }
        public List<CustomerPortalReceiptDto> Receipts { get; set; } = new();
    }

    public class CustomerPortalDashboardDto
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CompanyTitle { get; set; }
        public int TotalProjectsCount { get; set; }
        public int ActiveProjectsCount { get; set; }
        public int CompletedProjectsCount { get; set; }
        public decimal AverageProgressPercent { get; set; }
        public int InvoicesCount { get; set; }
        public decimal TotalInvoicesAmount { get; set; }
        public decimal NetOpenBalance { get; set; }
        public decimal PendingChequesAmount { get; set; }
        public List<CustomerPortalProjectSummaryDto> RecentProjects { get; set; } = new();
        public List<CustomerPortalProgressItemDto> RecentProgressLogs { get; set; } = new();
    }

    public class CustomerPortalProfileDto
    {
        public int CustomerId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserFirstName { get; set; } = string.Empty;
        public string UserLastName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? UserImageUrl { get; set; }
        public string? CompanyTitle { get; set; }
    }

    public class CustomerPortalProfileUpdateModel
    {
        public required string UserFirstName { get; set; }
        public required string UserLastName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
    }

    public class CustomerPortalChangePasswordModel
    {
        public required string CurrentPassword { get; set; }
        public required string NewPassword { get; set; }
        public required string ConfirmPassword { get; set; }
    }
}
