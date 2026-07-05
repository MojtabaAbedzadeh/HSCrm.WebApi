using Common.Permissions;
using HSCrm.Common.Permissions;
using HSCrm.DataModel.Context;
using HSCrm.Entities.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace HSCrm.DataModel.Seeders
{
    public static class PermissionSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext context)
        {
            var permissionsInCode = GetPermissionsFromCode();

            var existingPermissionNames = await context.Permissions.Select(p => p.Name).ToListAsync();

            var newPermissions = permissionsInCode.Where(p => !existingPermissionNames.Contains(p.Name)).ToList();

            if (newPermissions.Any())
            {
                await context.Permissions.AddRangeAsync(newPermissions);
                await context.SaveChangesAsync();
            }
        }
        private static List<Permission> GetPermissionsFromCode()
        {
            var result = new List<Permission>();

            var nestedTypes = typeof(Permissions).GetNestedTypes(BindingFlags.Public);

            foreach (var nestedType in nestedTypes)
            {
                var category = nestedType.Name;

                var fields = nestedType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string));

                foreach (var field in fields)
                {
                    var name = field.GetValue(null)?.ToString();

                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    var categoryInfo = PermissionCategories.Get(category);

                    result.Add(new Permission
                    {
                        Name = name,
                        Title = BuildTitle(categoryInfo.TitleFa, field.Name),
                        CategoryTitleEn = category,
                        CategoryTitleFa = categoryInfo.TitleFa,
                        CreatedAt = DateTime.UtcNow,
                        IsDeleted = false
                    });
                }
            }

            return result;
        }
        private static string BuildTitle(string categoryFa, string action)
        {
            var actionTitle = action switch
            {
                "View" => "مشاهده",
                "Create" => "ایجاد",
                "Edit" => "ویرایش",
                "Delete" => "حذف",
                _ => action
            };

            return $"{actionTitle} {categoryFa}";
        }
        public static async Task SeedRolePermissionsAsync(ApplicationDbContext context, RoleManager<ApplicationRole> roleManager)
        {
            var adminRole = await roleManager.FindByNameAsync("Admin");

            if (adminRole == null)
                return;

            var permissions = context.Permissions.ToList();

            foreach (var permission in permissions)
            {
                bool exists = context.RolePermissions.Any(rp => rp.RoleId == adminRole.Id && rp.PermissionId == permission.Id);

                if (!exists)
                {
                    context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = adminRole.Id,
                        PermissionId = permission.Id
                    });
                }
            }

            await context.SaveChangesAsync();
        }
    }
}