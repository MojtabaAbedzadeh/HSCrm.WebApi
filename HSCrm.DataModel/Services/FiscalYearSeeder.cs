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
            var persianMonth = persianCalendar.GetMonth(now);
            var persianDay = persianCalendar.GetDayOfMonth(now);

            /*
             اگر سال مالی تو از 1 فروردین شروع می‌شود:
             Start: 01/01
             End: 12/29 یا 12/30 بسته به کبیسه بودن
            */

            var startDate = persianCalendar.ToDateTime(
                persianYear,
                1,
                1,
                0,
                0,
                0,
                0
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

            var exists = await context.FiscalYears.AnyAsync(x =>
                x.TenantId == tenantId &&
                x.Title == title
            );

            if (exists)
                return;

            var currentFiscalYear = new FiscalYear
            {
                TenantId = tenantId,
                Title = title,
                StartDate = startDate,
                EndDate = endDate,
                IsCurrent = true,
                IsClosed = false,
                CreatedAt = DateTime.UtcNow
            };

            context.FiscalYears.Add(currentFiscalYear);

            await context.SaveChangesAsync();
        }
    }
}