namespace HSCrm.Entities.Entities
{
    public class AppNotification : TenantScopedEntity
    {
        public required string RecipientUserId { get; set; }
        public ApplicationUser? RecipientUser { get; set; }
        public required string Title { get; set; }
        public required string Message { get; set; }
        public string Category { get; set; } = "General";
        public string Severity { get; set; } = "info";
        public string? ReferenceEntity { get; set; }
        public int? ReferenceId { get; set; }
        public string? ActionUrl { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }
    }
}
