using System.ComponentModel.DataAnnotations;

namespace HSCrm.Entities.Models
{
    public class RoleListModel
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public string FaName { get; set; }
        public int TenantId { get; set; }
    }

    public class RoleCreateModel
    {
        [Required]
        public required string Name { get; set; }
        public required string FaName { get; set; }
        public int TenantId { get; set; }
    }

    public class RoleEditModel
    {
        [Required]
        public required string Id { get; set; }

        [Required]
        public required string Name { get; set; }

        public int TenantId { get; set; }
    }

    public class PermissionListModel
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Title { get; set; }
        public required string Category { get; set; }
    }

    public class RolePermissionsModel
    {
        public required string RoleId { get; set; }
        public List<int> PermissionIds { get; set; } = new();
    }
}
