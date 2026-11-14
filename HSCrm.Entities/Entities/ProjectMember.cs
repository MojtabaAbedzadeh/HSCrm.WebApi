using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HSCrm.Entities.Entities
{
    public class ProjectMember : TenantScopedEntity
    {
        public int ProjectId { get; set; }
        [JsonIgnore]
        public Project Project { get; set; } = default!;

        public required string UserId { get; set; }
        [JsonIgnore]
        public ApplicationUser User { get; set; } = default!;

        [MaxLength(100)]
        public string? ProjectRole { get; set; }

        public decimal? HourlyRate { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}
