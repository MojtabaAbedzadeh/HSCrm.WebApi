namespace HSCrm.Entities.Entities
{
    public class RolePermission
    {
        public string RoleId { get; set; } = null!;
        public ApplicationRole Role { get; set; } = null!;

        public int PermissionId { get; set; }
        public Permission Permission { get; set; } = null!;
    }
}