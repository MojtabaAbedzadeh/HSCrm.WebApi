using HSCrm.DataModel.Context;
using HSCrm.Entities.Entities;
using Microsoft.EntityFrameworkCore;

namespace HSCrm.DataModel.Services
{
    public static class RoleSeeder
    {
        public static async Task SeedRolesAsync(ApplicationDbContext context)
        {
            var roles = new List<ApplicationRole>
            {
                new ApplicationRole
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Admin",
                    NormalizedName = "ADMIN",
                    RoleFaName = "ادمین",
                    TenantId = 1
                },
                new ApplicationRole
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Owner",
                    NormalizedName = "OWNER",
                    RoleFaName = "مدیر",
                    TenantId = 1
                },
                new ApplicationRole
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "User",
                    NormalizedName = "USER",
                    RoleFaName = "کاربر",
                    TenantId = 1
                }
            };

            context.Roles.AddRange(roles);

            await context.SaveChangesAsync();
        }
        public static async Task AssignPermissionsToRoles(ApplicationDbContext context)
        {
            var ownerRole = await context.Roles.FirstAsync(r => r.Name == "Owner");

            var permissions = await context.Permissions.ToListAsync();

            foreach (var permission in permissions)
            {
                context.RolePermissions.Add(new RolePermission
                {
                    RoleId = ownerRole.Id,
                    PermissionId = permission.Id
                });
            }
        }
    }
}