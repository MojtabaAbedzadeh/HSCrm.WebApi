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
                    Name = "Customer",
                    NormalizedName = "CUSTOMER",
                    RoleFaName = "مشتری",
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
            var targetRoles = await context.Roles
                .Where(r => r.Name == "Owner" || r.Name == "Admin")
                .ToListAsync();

            var permissions = await context.Permissions.ToListAsync();
            var existingRolePermissions = await context.RolePermissions.ToListAsync();

            var userRoles = await context.Roles
                .Where(r => r.Name == "User" || r.NormalizedName == "USER")
                .ToListAsync();
            var baselineEmployeePermNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "projects.view",
                "workLogs.view",
                "workLogs.create",
                "workLogs.edit",
                "workLogs.delete"
            };
            var workLogPermissionIds = permissions
                .Where(p => p.Name.StartsWith("workLogs.", StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Id)
                .ToHashSet();

            foreach (var uRole in userRoles)
            {
                var hasAnyWorkLogPerm = existingRolePermissions.Any(rp =>
                    rp.RoleId == uRole.Id && workLogPermissionIds.Contains(rp.PermissionId));
                if (!hasAnyWorkLogPerm)
                {
                    foreach (var permission in permissions.Where(p => baselineEmployeePermNames.Contains(p.Name)))
                    {
                        var exists = existingRolePermissions.Any(rp => rp.RoleId == uRole.Id && rp.PermissionId == permission.Id);
                        if (!exists)
                        {
                            context.RolePermissions.Add(new RolePermission
                            {
                                RoleId = uRole.Id,
                                PermissionId = permission.Id
                            });
                        }
                    }
                }
            }

            foreach (var role in targetRoles)
            {
                foreach (var permission in permissions)
                {
                    var exists = existingRolePermissions.Any(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id);
                    if (!exists)
                    {
                        context.RolePermissions.Add(new RolePermission
                        {
                            RoleId = role.Id,
                            PermissionId = permission.Id
                        });
                    }
                }
            }
        }
    }
}