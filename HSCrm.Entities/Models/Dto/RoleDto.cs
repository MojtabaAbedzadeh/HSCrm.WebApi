namespace HSCrm.Entities.Models.Dto
{
    public class CreateRoleDto
    {
        public string Name { get; set; }
    }
    public class UpdateRoleDto
    {
        public string Name { get; set; }
    }
    public class AssignPermissionsDto
    {
        public string RoleId { get; set; }
        public List<string> PermissionNames { get; set; }
    }
    public class AssignUserRoleDto
    {
        public string UserId { get; set; }
        public string RoleId { get; set; }
    }
}