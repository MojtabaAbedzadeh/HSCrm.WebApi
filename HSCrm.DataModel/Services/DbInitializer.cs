using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using HSCrm.DataModel.Context;

namespace HSCrm.DataModel.Services
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            // ✅ اگر دیتابیس خالی است، کاری نکن
            if (!context.Tenants.Any())
                return;

            // ✅ آی‌دی ادمین Seed شده (همونی که در Identity داری)
            var adminUserId = "28dfc395-0464-4b47-8aae-c73953a9d153";

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
