using HSCrm.Entities.Entities;
using Microsoft.AspNetCore.Identity;

public class ApplicationRole : IdentityRole<string>
{
    public string RoleFaName { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = default!;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}