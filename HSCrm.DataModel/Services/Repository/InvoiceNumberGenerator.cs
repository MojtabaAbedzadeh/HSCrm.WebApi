using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace HSCrm.DataModel.Services.Repository
{
    public class InvoiceNumberGenerator : IInvoiceNumberGenerator
    {
        private readonly ApplicationDbContext _context;
        private static readonly PersianCalendar _persianCalendar = new();

        public InvoiceNumberGenerator(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string> GenerateNextSalesInvoiceNumberAsync(int tenantId, int? fiscalYearId, DateTime? issueDate = null)
        {
            var date = issueDate ?? DateTime.UtcNow;
            var tenant = await _context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId);
            var pattern = !string.IsNullOrWhiteSpace(tenant?.SalesInvoicePattern)
                ? tenant.SalesInvoicePattern
                : "INV-{YEAR}-{SEQ:4}";

            var baseCount = await _context.SalesInvoices
                .IgnoreQueryFilters()
                .CountAsync(i => i.TenantId == tenantId && (fiscalYearId == null || i.FiscalYearId == fiscalYearId.Value));

            int seq = baseCount + 1;
            string candidate;
            do
            {
                candidate = FormatNumber(pattern, "INV", seq, date, tenant?.TenantCode);
                bool exists = await _context.SalesInvoices
                    .IgnoreQueryFilters()
                    .AnyAsync(i => i.TenantId == tenantId && i.Number == candidate);

                if (!exists)
                    break;

                seq++;
            } while (seq < baseCount + 10000);

            return candidate;
        }

        public async Task<string> GenerateNextPurchaseInvoiceNumberAsync(int tenantId, int? fiscalYearId, DateTime? issueDate = null)
        {
            var date = issueDate ?? DateTime.UtcNow;
            var tenant = await _context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId);
            var pattern = !string.IsNullOrWhiteSpace(tenant?.PurchaseInvoicePattern)
                ? tenant.PurchaseInvoicePattern
                : "PINV-{YEAR}-{SEQ:4}";

            var baseCount = await _context.PurchaseInvoices
                .IgnoreQueryFilters()
                .CountAsync(i => i.TenantId == tenantId && (fiscalYearId == null || i.FiscalYearId == fiscalYearId.Value));

            int seq = baseCount + 1;
            string candidate;
            do
            {
                candidate = FormatNumber(pattern, "PINV", seq, date, tenant?.TenantCode);
                bool exists = await _context.PurchaseInvoices
                    .IgnoreQueryFilters()
                    .AnyAsync(i => i.TenantId == tenantId && i.Number == candidate);

                if (!exists)
                    break;

                seq++;
            } while (seq < baseCount + 10000);

            return candidate;
        }

        public async Task<string> GenerateNextSalesReturnNumberAsync(int tenantId, int? fiscalYearId, DateTime? issueDate = null)
        {
            var date = issueDate ?? DateTime.UtcNow;
            var tenant = await _context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId);
            var pattern = !string.IsNullOrWhiteSpace(tenant?.SalesReturnPattern)
                ? tenant.SalesReturnPattern
                : "SRI-{YEAR}-{SEQ:4}";

            var baseCount = await _context.SalesReturnInvoices
                .IgnoreQueryFilters()
                .CountAsync(i => i.TenantId == tenantId && (fiscalYearId == null || i.FiscalYearId == fiscalYearId.Value));

            int seq = baseCount + 1;
            string candidate;
            do
            {
                candidate = FormatNumber(pattern, "SRI", seq, date, tenant?.TenantCode);
                bool exists = await _context.SalesReturnInvoices
                    .IgnoreQueryFilters()
                    .AnyAsync(i => i.TenantId == tenantId && i.Number == candidate);

                if (!exists)
                    break;

                seq++;
            } while (seq < baseCount + 10000);

            return candidate;
        }

        public async Task<string> GenerateNextPurchaseReturnNumberAsync(int tenantId, int? fiscalYearId, DateTime? issueDate = null)
        {
            var date = issueDate ?? DateTime.UtcNow;
            var tenant = await _context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId);
            var pattern = !string.IsNullOrWhiteSpace(tenant?.PurchaseReturnPattern)
                ? tenant.PurchaseReturnPattern
                : "PRI-{YEAR}-{SEQ:4}";

            var baseCount = await _context.PurchaseReturnInvoices
                .IgnoreQueryFilters()
                .CountAsync(i => i.TenantId == tenantId && (fiscalYearId == null || i.FiscalYearId == fiscalYearId.Value));

            int seq = baseCount + 1;
            string candidate;
            do
            {
                candidate = FormatNumber(pattern, "PRI", seq, date, tenant?.TenantCode);
                bool exists = await _context.PurchaseReturnInvoices
                    .IgnoreQueryFilters()
                    .AnyAsync(i => i.TenantId == tenantId && i.Number == candidate);

                if (!exists)
                    break;

                seq++;
            } while (seq < baseCount + 10000);

            return candidate;
        }

        public string FormatNumber(string pattern, string defaultPrefix, int sequenceNumber, DateTime date, string? tenantCode = null)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                pattern = $"{defaultPrefix}-{YEAR}-{SEQ:4}";

            int pYear = _persianCalendar.GetYear(date);
            int pMonth = _persianCalendar.GetMonth(date);
            int pDay = _persianCalendar.GetDayOfMonth(date);

            string result = pattern;
            result = result.Replace("{PREFIX}", defaultPrefix, StringComparison.OrdinalIgnoreCase);
            result = result.Replace("{YEAR}", pYear.ToString("0000"), StringComparison.OrdinalIgnoreCase);
            result = result.Replace("{YY}", (pYear % 100).ToString("00"), StringComparison.OrdinalIgnoreCase);
            result = result.Replace("{MONTH}", pMonth.ToString("00"), StringComparison.OrdinalIgnoreCase);
            result = result.Replace("{DAY}", pDay.ToString("00"), StringComparison.OrdinalIgnoreCase);
            result = result.Replace("{TENANT}", tenantCode ?? "T", StringComparison.OrdinalIgnoreCase);

            // جایگزینی {SEQ} یا {SEQ:N} مثلاً {SEQ:4}
            result = Regex.Replace(result, @"\{SEQ(?::(\d+))?\}", m =>
            {
                if (m.Groups[1].Success && int.TryParse(m.Groups[1].Value, out int padLength) && padLength > 0 && padLength <= 10)
                {
                    return sequenceNumber.ToString(new string('0', padLength));
                }
                return sequenceNumber.ToString();
            }, RegexOptions.IgnoreCase);

            return result;
        }
    }
}
