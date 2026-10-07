using HSCrm.DataModel.Context;
using HSCrm.DataModel.Seeders;
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

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

            await context.Database.MigrateAsync();

            // 1) Seed permissions
            if (!await context.Permissions.AnyAsync())
            {
                await PermissionSeeder.SeedAsync(context);
            }

            // 2) Seed roles
            if (!await context.Roles.AnyAsync())
            {
                await RoleSeeder.SeedRolesAsync(context);
            }

            // 3) Make sure required roles exist even if Roles table was partially seeded
            await EnsureRoleExistsAsync(roleManager, "Admin");
            await EnsureRoleExistsAsync(roleManager, "Owner");
            await EnsureRoleExistsAsync(roleManager, "User");
            await EnsureRoleExistsAsync(roleManager, "Customer");

            // 4) Seed role-permissions
            if (!await context.RolePermissions.AnyAsync())
            {
                await RoleSeeder.AssignPermissionsToRoles(context);
            }

            // 5) If there is a tenant, seed fiscal year safely
            const string adminUserId = "28dfc395-0464-4b47-8aae-c73953a9d153";

            var tenant = await context.Tenants.FirstOrDefaultAsync();
            if (tenant != null)
            {
                await FiscalYearSeeder.SeedCurrentFiscalYearAsync(context, tenant.Id);

                if (string.IsNullOrWhiteSpace(tenant.CreatedById))
                    tenant.CreatedById = adminUserId;

                var fiscalYear = await context.FiscalYears.FirstOrDefaultAsync();
                if (fiscalYear != null && string.IsNullOrWhiteSpace(fiscalYear.CreatedById))
                    fiscalYear.CreatedById = adminUserId;
            }

            await context.SaveChangesAsync();
        }

        public static async Task SeedAsync(ApplicationDbContext context)
        {
            if (!await context.Permissions.AnyAsync())
            {
                await PermissionSeeder.SeedAsync(context);
            }

            if (!await context.Roles.AnyAsync())
            {
                await RoleSeeder.SeedRolesAsync(context);
            }

            if (!await context.RolePermissions.AnyAsync())
            {
                await RoleSeeder.AssignPermissionsToRoles(context);
            }

            await context.SaveChangesAsync();
        }

        private static async Task EnsureRoleExistsAsync(RoleManager<ApplicationRole> roleManager, string roleName)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new ApplicationRole
                {
                    Id = Guid.NewGuid().ToString(),   // ✅ مهم
                    Name = roleName,
                    NormalizedName = roleName.ToUpperInvariant(),
                    ConcurrencyStamp = Guid.NewGuid().ToString()
                });
            }
        }
    }
}
