using HSCrm.DataModel.Context;
using HSCrm.Entities.Entities;
using Microsoft.EntityFrameworkCore;

namespace HSCrm.DataModel.Services
{
    public static class RoleSeeder
    {
        // نقش‌های پایه که برای هر Tenant جدید باید ساخته شوند
        private static readonly Dictionary<string, string> DefaultRoles = new()
        {
            { "Owner", "مدیر" },
            { "Admin", "ادمین" },
            { "User", "کاربر" },
            { "Customer", "مشتری" }
        };

        public static async Task SeedRolesAsync(ApplicationDbContext context, int tenantId)
        {
            // گرفتن نام نقش‌های موجود برای این Tenant خاص
            var existingRoleNames = await context.Roles
                .Where(r => r.TenantId == tenantId)
                .Select(r => r.Name)
                .ToListAsync();

            var newRoles = new List<ApplicationRole>();

            foreach (var role in DefaultRoles)
            {
                if (!existingRoleNames.Contains(role.Key))
                {
                    newRoles.Add(new ApplicationRole
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = role.Key,
                        NormalizedName = role.Key.ToUpperInvariant(),
                        RoleFaName = role.Value,
                        TenantId = tenantId
                    });
                }
            }

            if (newRoles.Any())
            {
                await context.Roles.AddRangeAsync(newRoles);
                await context.SaveChangesAsync();
            }
        }

        public static async Task AssignPermissionsToRolesAsync(ApplicationDbContext context, int tenantId)
        {
            // دریافت تمام نقش‌های این Tenant
            var roles = await context.Roles
                .Where(r => r.TenantId == tenantId)
                .ToDictionaryAsync(r => r.Name!);

            // دریافت تمام مجوزهای سیستم (فرض بر این است که Permissions سراسری هستند)
            var allPermissions = await context.Permissions.ToListAsync();

            foreach (var role in roles)
            {
                var roleId = role.Value.Id;
                var permissionIds = GetPermissionsForRole(role.Key, allPermissions);

                foreach (var permissionId in permissionIds)
                {
                    // جلوگیری از اضافه کردن تکراری
                    bool exists = await context.RolePermissions
                        .AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);

                    if (!exists)
                    {
                        await context.RolePermissions.AddAsync(new RolePermission
                        {
                            RoleId = roleId,
                            PermissionId = permissionId
                        });
                    }
                }
            }

            await context.SaveChangesAsync();
        }

        private static List<int> GetPermissionsForRole(string roleName, List<Permission> allPermissions)
        {
            // اینجا بر اساس نام نقش، مجوزهای خاص خودش را برمی‌گردانیم
            return roleName switch
            {
                "Owner" => allPermissions.Select(p => p.Id).ToList(), // Owner همه چیز را دارد

                "Admin" => allPermissions
                    .Where(p => !p.Name.Contains("System")) // فرض: ادمین به بخش‌های System دسترسی ندارد
                    .Select(p => p.Id).ToList(),

                "User" => allPermissions
                    .Where(p => p.Name.Contains("View") || p.Name.Contains("Create")) // فقط دیدن و ایجاد
                    .Select(p => p.Id).ToList(),

                "Customer" => allPermissions
                    .Where(p => p.Name.StartsWith("Customer")) // فقط دسترسی‌های مشتری
                    .Select(p => p.Id).ToList(),

                _ => new List<int>()
            };
        }
    }
}