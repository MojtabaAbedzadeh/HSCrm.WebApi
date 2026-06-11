namespace HSCrm.Entities.Entities
{
    public class Permission : AuditedEntity
    {
        public string Name { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string Category { get; set; } = null!;

        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}