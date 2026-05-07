using HSCrm.Entities.Entities;

namespace HSCrm.Entities.Models
{
    public class ProjectCreateModel
    {
        public required string ProjectTitle { get; set; }
        public int CustomerId { get; set; }
        public string? ProjectLocation { get; set; }
        public required string CreateById { get; set; }
        public int TenantId { get; set; }
    }

    public class ProjectEditModel
    {
        public int ProjectId { get; set; }        
        public required string ProjectTitle { get; set; }
        public int CustomerId { get; set; }
        public string? ProjectLocation { get; set; }
        public required string ModifiedById { get; set; }
    }
}
