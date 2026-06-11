using Common.Permissions;
using HSCrm.DataModel.Context;
using HSCrm.Entities.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Reflection;

namespace HSCrm.DataModel.Seeders
{
    public static class PermissionSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext context)
        {
            var permissionsInCode = GetPermissionsFromCode();

            var existingPermissionNames = await context.Permissions
                .Select(p => p.Name)
                .ToListAsync();

            var newPermissions = permissionsInCode
                .Where(p => !existingPermissionNames.Contains(p.Name))
                .ToList();

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

                var fields = nestedType
                    .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                    .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string));

                foreach (var field in fields)
                {
                    var name = field.GetValue(null)?.ToString();

                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    result.Add(new Permission
                    {
                        Name = name,
                        Title = BuildTitle(category, field.Name),
                        Category = category,
                        CreatedAt = DateTime.UtcNow,
                        IsDeleted = false
                    });
                }
            }

            return result;
        }

        private static string BuildTitle(string category, string action)
        {
            var actionTitle = action switch
            {
                "View" => "مشاهده",
                "Create" => "ایجاد",
                "Edit" => "ویرایش",
                "Delete" => "حذف",
                _ => action
            };

            var categoryTitle = category switch
            {
                "Users" => "کاربران",
                "Roles" => "نقش‌ها",
                "Customers" => "مشتریان",
                "Suppliers" => "تأمین‌کنندگان",
                "Expenses" => "هزینه‌ها",
                "FiscalYears" => "سال‌های مالی",
                "Inventories" => "انبارها",
                "Payments" => "پرداخت‌ها",
                "Products" => "محصولات",
                "ProductPrices" => "قیمت محصولات",
                "Projects" => "پروژه‌ها",
                "PurchaseInvoices" => "فاکتورهای خرید",
                "RolePermissions" => "دسترسی‌های نقش",
                "SalesInvoices" => "فاکتورهای فروش",
                "Tenants" => "مستأجرها",
                "UserPermissions" => "دسترسی‌های کاربر",
                "UserRoles" => "نقش‌های کاربر",
                "Warehouses" => "انبارها",
                _ => category
            };

            return $"{actionTitle} {categoryTitle}";
        }
    }
}
