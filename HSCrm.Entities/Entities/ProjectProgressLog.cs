using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HSCrm.Entities.Entities
{
    public class ProjectProgressLog : TenantScopedEntity, IFiscalYearScoped
    {
        public int ProjectId { get; set; }
        [JsonIgnore]
        public Project Project { get; set; } = default!;

        public int? MilestoneId { get; set; }
        [JsonIgnore]
        public ProjectMilestone? Milestone { get; set; }

        public int FiscalYearId { get; set; }
        [JsonIgnore]
        public FiscalYear FiscalYear { get; set; } = default!;

        public required string RecordedByUserId { get; set; }
        [JsonIgnore]
        public ApplicationUser RecordedByUser { get; set; } = default!;

        public DateTime LogDate { get; set; }

        [MaxLength(200)]
        public required string Title { get; set; }

        [MaxLength(2000)]
        public required string ReportText { get; set; }

        public decimal ProgressPercent { get; set; }

        [MaxLength(300)]
        public string? WorkshopArea { get; set; }
    }
}
