using HSCrm.Entities.Entities;

namespace HSCrm.Entities.Models
{
    public class ProjectCreateModel
    {
        public required string ProjectTitle { get; set; }
        public required string ProjectOwner { get; set; }
        public string? ProjectOwnerPhone { get; set; }
        public string? ProjectLocation { get; set; }
        public required string UserId { get; set; }
        public ApplicationUser User { get; set; } = default!;
        public DateTime CreatedDate { get; set; }
    }

    public class ProjectEditModel
    {
        public int ProjectId { get; set; }
        public required string ProjectTitle { get; set; }
        public required string ProjectOwner { get; set; }
        public string? ProjectOwnerPhone { get; set; }
        public string? ProjectLocation { get; set; }
        public required string UserId { get; set; }
        public ApplicationUser User { get; set; } = default!;
        public DateTime CreatedDate { get; set; }
    }
}
