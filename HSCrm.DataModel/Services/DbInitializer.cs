using HSCrm.DataModel.Context;
using HSCrm.DataModel.Seeders;
using HSCrm.Entities.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HSCrm.DataModel.Services
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            var roleManager = scope.ServiceProvider
                .GetRequiredService<RoleManager<ApplicationRole>>();

            // ✅ اگر دیتابیس خالی است، کاری نکن
            if (!context.Tenants.Any())
                return;

            // ✅ آی‌دی ادمین Seed شده
            var adminUserId = "28dfc395-0464-4b47-8aae-c73953a9d153";

            // ✅ سید دسترسی‌ها
            await PermissionSeeder.SeedRolePermissionsAsync(context, roleManager);

            var tenant = await context.Tenants.FirstAsync();
            if (tenant.CreatedById == null)
                tenant.CreatedById = adminUserId;

            var fiscalYear = await context.FiscalYears.FirstAsync();
            if (fiscalYear.CreatedById == null)
                fiscalYear.CreatedById = adminUserId;

            await context.SaveChangesAsync();
        }
    }
}