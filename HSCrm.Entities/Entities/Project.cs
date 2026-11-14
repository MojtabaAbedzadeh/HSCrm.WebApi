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

        [JsonIgnore]
        public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();

        [JsonIgnore]
        public ICollection<ProjectWorkLog> WorkLogs { get; set; } = new List<ProjectWorkLog>();
    }
}
