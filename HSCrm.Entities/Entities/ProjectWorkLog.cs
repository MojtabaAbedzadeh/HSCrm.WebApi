using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Entities.Entities
{
    public class ProjectWorkLog : TenantScopedEntity, IFiscalYearScoped
    {
        public int ProjectId { get; set; }
        [JsonIgnore]
        public Project Project { get; set; } = default!;

        public required string UserId { get; set; }
        [JsonIgnore]
        public ApplicationUser User { get; set; } = default!;

        public int FiscalYearId { get; set; }
        [JsonIgnore]
        public FiscalYear FiscalYear { get; set; } = default!;

        public DateTime WorkDate { get; set; }

        public decimal Hours { get; set; }

        public decimal HourlyRateSnapshot { get; set; }

        public decimal WageCost { get; set; }

        [MaxLength(1000)]
        public required string Description { get; set; }

        public WorkLogStatus Status { get; set; } = WorkLogStatus.Pending;

        public string? ReviewedByUserId { get; set; }
        [JsonIgnore]
        public ApplicationUser? ReviewedByUser { get; set; }

        public DateTime? ReviewedAt { get; set; }

        [MaxLength(500)]
        public string? ReviewNote { get; set; }
    }
}
