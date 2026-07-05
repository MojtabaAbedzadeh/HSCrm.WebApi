using HSCrm.DataModel.Context;
using HSCrm.Entities.Entities;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace HSCrm.DataModel.Seeders
{
    public static class FiscalYearSeeder
    {
        public static async Task SeedCurrentFiscalYearAsync(ApplicationDbContext context, int tenantId)
        {
            var persianCalendar = new PersianCalendar();
            var now = DateTime.Now;

            var persianYear = persianCalendar.GetYear(now);

            var startDate = persianCalendar.ToDateTime(
                persianYear, 1, 1, 0, 0, 0, 0
            );

            var lastDayOfYear = persianCalendar.GetDaysInMonth(persianYear, 12);

            var endDate = persianCalendar.ToDateTime(
                persianYear,
                12,
                lastDayOfYear,
                23,
                59,
                59,
                999
            );

            var title = persianYear.ToString();

            // ✅ مهم — بدون QueryFilter
            var fiscalYears = await context.FiscalYears
                .IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId)
                .ToListAsync();

            var existingYear = fiscalYears.FirstOrDefault(x => x.Title == title);

            if (existingYear != null)
            {
                // اگر سال وجود دارد ولی Current نیست
                if (!existingYear.IsCurrent)
                {
                    foreach (var fy in fiscalYears.Where(x => x.IsCurrent))
                        fy.IsCurrent = false;

                    existingYear.IsCurrent = true;

                    await context.SaveChangesAsync();
                }

                return;
            }

            // ✅ اگر سال جدید است
            foreach (var fy in fiscalYears.Where(x => x.IsCurrent))
                fy.IsCurrent = false;

            var newFiscalYear = new FiscalYear
            {
                TenantId = tenantId,
                Title = title,
                StartDate = startDate,
                EndDate = endDate,
                IsCurrent = true,
                IsClosed = false,
                CreatedAt = DateTime.UtcNow
            };

            context.FiscalYears.Add(newFiscalYear);

            await context.SaveChangesAsync();
        }
    }
}