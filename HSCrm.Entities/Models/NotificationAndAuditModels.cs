namespace HSCrm.Entities.Models
{
    public class NotificationDto
    {
        public int? Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
        public string Severity { get; set; } = "info";
        public string? ReferenceEntity { get; set; }
        public int? ReferenceId { get; set; }
        public string? ActionUrl { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedAtShamsi { get; set; } = string.Empty;
        public bool IsDynamicAlert { get; set; }
    }

    public class NotificationFeedDto
    {
        public int UnreadCount { get; set; }
        public List<NotificationDto> Items { get; set; } = new();
    }

    public class ProjectStatusUpdateModel
    {
        public int ProjectId { get; set; }
        public required string Status { get; set; }
        public string? Note { get; set; }
    }

    public class AuditTrailItemDto
    {
        public string EntityType { get; set; } = string.Empty;
        public string EntityTitleFa { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string DocumentNumberOrTitle { get; set; } = string.Empty;
        public string? ProjectOrPartyTitle { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedAtShamsi { get; set; } = string.Empty;
        public string? CreatedById { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime? ModifiedAt { get; set; }
        public string? ModifiedAtShamsi { get; set; }
        public string? ModifiedById { get; set; }
        public string? ModifiedByName { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedAtShamsi { get; set; }
    }

    public class AuditTrailQueryResultDto
    {
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<AuditTrailItemDto> Items { get; set; } = new();
    }
}
