using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Entities.Models
{
    public class ProjectMilestoneDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string ProjectTitle { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SortOrder { get; set; }
        public decimal WeightPercent { get; set; }
        public decimal ProgressPercent { get; set; }
        public DateTime? DueDate { get; set; }
        public string? DueDateShamsi { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? CompletedAtShamsi { get; set; }
        public MilestoneStatus Status { get; set; }
        public string StatusTitle { get; set; } = string.Empty;
        public int LogsCount { get; set; }
    }

    public class ProjectMilestoneCreateModel
    {
        public int ProjectId { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        public int SortOrder { get; set; }
        public decimal WeightPercent { get; set; }
        public decimal ProgressPercent { get; set; }
        public string? DueDate { get; set; }
    }

    public class ProjectMilestoneUpdateModel
    {
        public int Id { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        public int SortOrder { get; set; }
        public decimal WeightPercent { get; set; }
        public decimal ProgressPercent { get; set; }
        public string? DueDate { get; set; }
        public MilestoneStatus? Status { get; set; }
    }

    public class ProjectAttachmentDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string StoragePath { get; set; } = string.Empty;
        public bool IsImage { get; set; }
        public int ProgressLogId { get; set; }
        public string ProgressLogTitle { get; set; } = string.Empty;
        public string LogDateShamsi { get; set; } = string.Empty;
        public string UploadedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class ProjectProgressLogDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string ProjectTitle { get; set; } = string.Empty;
        public int? MilestoneId { get; set; }
        public string? MilestoneTitle { get; set; }
        public int FiscalYearId { get; set; }
        public string RecordedByUserId { get; set; } = string.Empty;
        public string RecordedByName { get; set; } = string.Empty;
        public DateTime LogDate { get; set; }
        public string LogDateShamsi { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ReportText { get; set; } = string.Empty;
        public decimal ProgressPercent { get; set; }
        public string? WorkshopArea { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<ProjectAttachmentDto> Attachments { get; set; } = new();
    }

    public class ProjectAttachmentUpload
    {
        public required string FileName { get; set; }
        public required string ContentType { get; set; }
        public long Length { get; set; }
        public required byte[] Content { get; set; }
    }

    public class ProjectProgressLogCreateInput
    {
        public int ProjectId { get; set; }
        public int? MilestoneId { get; set; }
        public required string LogDate { get; set; }
        public required string Title { get; set; }
        public required string ReportText { get; set; }
        public decimal ProgressPercent { get; set; }
        public string? WorkshopArea { get; set; }
        public List<ProjectAttachmentUpload> Files { get; set; } = new();
    }

    public class ProjectProfitAndLossInvoiceItemDto
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; }
        public string IssueDateShamsi { get; set; } = string.Empty;
        public InvoiceStatus Status { get; set; }
        public string StatusTitle { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public decimal MaterialCost { get; set; }
        public string? Description { get; set; }
    }

    public class ProjectProfitAndLossExpenseItemDto
    {
        public int Id { get; set; }
        public string ExpenseTitle { get; set; } = string.Empty;
        public string TypeTitle { get; set; } = string.Empty;
        public DateTime ExpenseDate { get; set; }
        public string ExpenseDateShamsi { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? Description { get; set; }
    }

    public class ProjectProfitAndLossMemberWageDto
    {
        public string UserId { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string? ProjectRole { get; set; }
        public decimal ApprovedHours { get; set; }
        public decimal ApprovedWageCost { get; set; }
        public decimal PendingHours { get; set; }
        public decimal PendingWageCostEstimate { get; set; }
    }

    public class ProjectProfitAndLossMaterialItemDto
    {
        public int ProductId { get; set; }
        public string ProductTitle { get; set; } = string.Empty;
        public string? ProductUnit { get; set; }
        public decimal SoldQuantity { get; set; }
        public decimal ReturnedQuantity { get; set; }
        public decimal NetQuantity { get; set; }
        public decimal NetSalesAmount { get; set; }
        public decimal NetCogsAmount { get; set; }
    }

    public class ProjectProfitAndLossDto
    {
        public int ProjectId { get; set; }
        public string ProjectTitle { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public string? ProjectLocation { get; set; }
        public string Status { get; set; } = "Active";
        public decimal ProgressPercent { get; set; }

        public decimal GrossSalesRevenue { get; set; }
        public decimal SalesReturnsAmount { get; set; }
        public decimal NetSalesRevenue { get; set; }
        public decimal ProformaSalesTotal { get; set; }
        public int FinalInvoicesCount { get; set; }
        public int ReturnInvoicesCount { get; set; }
        public int ProformaInvoicesCount { get; set; }

        public decimal GrossMaterialCogs { get; set; }
        public decimal ReturnedMaterialCogs { get; set; }
        public decimal NetMaterialCogs { get; set; }
        public decimal TotalDirectExpenses { get; set; }
        public decimal TotalApprovedLaborCost { get; set; }
        public decimal TotalApprovedHours { get; set; }
        public decimal PendingLaborCostEstimate { get; set; }
        public decimal PendingHours { get; set; }

        public decimal TotalCosts { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal NetProfit { get; set; }
        public decimal ProfitMarginPercent { get; set; }

        public decimal? EstimatedBudget { get; set; }
        public decimal? BudgetVariance { get; set; }
        public decimal? BudgetUsedPercent { get; set; }
        public bool IsOverBudget { get; set; }

        public List<ProjectProfitAndLossInvoiceItemDto> Invoices { get; set; } = new();
        public List<ProjectProfitAndLossInvoiceItemDto> ReturnInvoices { get; set; } = new();
        public List<ProjectProfitAndLossExpenseItemDto> DirectExpenses { get; set; } = new();
        public List<ProjectProfitAndLossMemberWageDto> LaborByMembers { get; set; } = new();
        public List<ProjectProfitAndLossMaterialItemDto> MaterialItems { get; set; } = new();
        public List<ProjectMilestoneDto> Milestones { get; set; } = new();
    }
}
