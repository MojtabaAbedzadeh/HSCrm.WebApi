using System.Text.Json.Serialization;

namespace HSCrm.Entities.Entities
{
    public class Project : TenantScopedEntity
    {
        public required string ProjectTitle { get; set; }
        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public string? ProjectLocation { get; set; }
        public string Status { get; set; } = "Active";
        public decimal? EstimatedBudget { get; set; }
        public decimal ProgressPercent { get; set; } = 0m;

        [JsonIgnore]
        public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();

        [JsonIgnore]
        public ICollection<ProjectWorkLog> WorkLogs { get; set; } = new List<ProjectWorkLog>();

        [JsonIgnore]
        public ICollection<ProjectMilestone> Milestones { get; set; } = new List<ProjectMilestone>();

        [JsonIgnore]
        public ICollection<ProjectProgressLog> ProgressLogs { get; set; } = new List<ProjectProgressLog>();
    }
}
