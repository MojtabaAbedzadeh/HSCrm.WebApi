using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Entities.Entities
{
    public class ProjectMilestone : TenantScopedEntity
    {
        public int ProjectId { get; set; }
        [JsonIgnore]
        public Project Project { get; set; } = default!;

        [MaxLength(200)]
        public required string Title { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public int SortOrder { get; set; }
        public decimal WeightPercent { get; set; } = 0m;
        public decimal ProgressPercent { get; set; } = 0m;
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedAt { get; set; }
        public MilestoneStatus Status { get; set; } = MilestoneStatus.NotStarted;

        [JsonIgnore]
        public ICollection<ProjectProgressLog> ProgressLogs { get; set; } = new List<ProjectProgressLog>();
    }
}
