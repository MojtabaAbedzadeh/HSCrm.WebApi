using System.ComponentModel.DataAnnotations;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Entities.Models
{
    public class ProjectMemberDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string ProjectTitle { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string? UserImageUrl { get; set; }
        public string? ProjectRole { get; set; }
        public decimal? HourlyRate { get; set; }
        public bool IsActive { get; set; }
        public DateTime JoinedAt { get; set; }
        public string JoinedAtShamsi { get; set; } = string.Empty;
    }

    public class AssignableUserDto
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public byte UserType { get; set; }
        public string? UserImageUrl { get; set; }
    }

    public class ProjectMemberUpsertModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "شناسه پروژه نامعتبر است.")]
        public int ProjectId { get; set; }

        [Required(ErrorMessage = "انتخاب کاربر الزامی است.")]
        public required string UserId { get; set; }

        [MaxLength(100)]
        public string? ProjectRole { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "نرخ ساعتی نمی‌تواند منفی باشد.")]
        public decimal? HourlyRate { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class ProjectWorkLogDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string ProjectTitle { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public int FiscalYearId { get; set; }
        public DateTime WorkDate { get; set; }
        public string WorkDateShamsi { get; set; } = string.Empty;
        public decimal Hours { get; set; }
        public decimal HourlyRateSnapshot { get; set; }
        public decimal WageCost { get; set; }
        public string Description { get; set; } = string.Empty;
        public WorkLogStatus Status { get; set; }
        public string StatusTitle { get; set; } = string.Empty;
        public string? ReviewedByUserId { get; set; }
        public string? ReviewedByName { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewedAtShamsi { get; set; }
        public string? ReviewNote { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ProjectWorkLogCreateModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "انتخاب پروژه الزامی است.")]
        public int ProjectId { get; set; }

        [Required(ErrorMessage = "تاریخ کارکرد الزامی است.")]
        public required string WorkDate { get; set; }

        [Range(0.25, 24, ErrorMessage = "ساعت کار باید بین ۰٫۲۵ تا ۲۴ ساعت باشد.")]
        public decimal Hours { get; set; }

        [Required(ErrorMessage = "شرح فعالیت انجام‌شده الزامی است.")]
        [MaxLength(1000)]
        public required string Description { get; set; }
    }

    public class ProjectWorkLogEditModel
    {
        [Range(1, int.MaxValue)]
        public int Id { get; set; }

        [Range(1, int.MaxValue)]
        public int ProjectId { get; set; }

        [Required(ErrorMessage = "تاریخ کارکرد الزامی است.")]
        public required string WorkDate { get; set; }

        [Range(0.25, 24, ErrorMessage = "ساعت کار باید بین ۰٫۲۵ تا ۲۴ ساعت باشد.")]
        public decimal Hours { get; set; }

        [Required(ErrorMessage = "شرح فعالیت انجام‌شده الزامی است.")]
        [MaxLength(1000)]
        public required string Description { get; set; }
    }

    public class ProjectWorkLogReviewModel
    {
        [Range(1, int.MaxValue)]
        public int WorkLogId { get; set; }

        public bool IsApproved { get; set; }

        [MaxLength(500)]
        public string? ReviewNote { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? OverrideHourlyRate { get; set; }
    }

    public class ProjectWorkLogBatchReviewModel
    {
        [Required]
        public List<int> WorkLogIds { get; set; } = new();

        public bool IsApproved { get; set; }

        [MaxLength(500)]
        public string? ReviewNote { get; set; }
    }

    public class EmployeeProjectSummaryDto
    {
        public int ProjectId { get; set; }
        public string ProjectTitle { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public string? ProjectLocation { get; set; }
        public string? ProjectRole { get; set; }
        public decimal? HourlyRate { get; set; }
        public decimal MyTotalHours { get; set; }
        public decimal MyApprovedHours { get; set; }
        public decimal MyPendingHours { get; set; }
        public decimal MyApprovedWageCost { get; set; }
    }

    public class EmployeeDashboardDto
    {
        public bool IsFullAccess { get; set; }
        public int AssignedProjectsCount { get; set; }
        public decimal TotalLoggedHours { get; set; }
        public decimal ApprovedHours { get; set; }
        public decimal PendingHours { get; set; }
        public decimal RejectedHours { get; set; }
        public decimal ApprovedWageCost { get; set; }
        public List<EmployeeProjectSummaryDto> Projects { get; set; } = new();
        public List<ProjectWorkLogDto> RecentWorkLogs { get; set; } = new();
    }
}
