namespace HSCrm.Entities.Entities
{
    public class Permission : AuditedEntity
    {
        public string Name { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string CategoryTitleEn { get; set; } = null!;
        public string CategoryTitleFa { get; set; } = null!;

        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}