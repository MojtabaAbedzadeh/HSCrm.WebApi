using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using Microsoft.EntityFrameworkCore;
using SinaPharm.Common;

namespace HSCrm.DataModel.Services.Repository
{
    public class AuditTrailService : IAuditTrailService
    {
        private readonly ApplicationDbContext _context;

        public AuditTrailService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AuditTrailQueryResultDto> GetAuditLogsAsync(
            int tenantId,
            string? entityType = null,
            string? userId = null,
            string? search = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            bool includeDeleted = true,
            int page = 1,
            int pageSize = 50,
            CancellationToken ct = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 200);

            var allItems = new List<AuditTrailItemDto>();
            var typeFilter = string.IsNullOrWhiteSpace(entityType) ? null : entityType.Trim();

            bool ShouldLoad(string name) =>
                string.IsNullOrEmpty(typeFilter) || string.Equals(typeFilter, name, StringComparison.OrdinalIgnoreCase);

            if (ShouldLoad("SalesInvoice"))
            {
                var rows = await _context.SalesInvoices.AsNoTracking().IgnoreQueryFilters()
                    .Include(x => x.Project)
                    .Where(x => x.TenantId == tenantId && (includeDeleted || !x.IsDeleted))
                    .Select(x => new RawAuditRow("SalesInvoice", "فاکتور فروش", x.Id, "فاکتور #" + x.Number, x.Project != null ? x.Project.ProjectTitle : null, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .ToListAsync(ct);
                allItems.AddRange(MapRaw(rows));
            }

            if (ShouldLoad("PurchaseInvoice"))
            {
                var rows = await _context.PurchaseInvoices.AsNoTracking().IgnoreQueryFilters()
                    .Include(x => x.Supplier)
                    .Where(x => x.TenantId == tenantId && (includeDeleted || !x.IsDeleted))
                    .Select(x => new RawAuditRow("PurchaseInvoice", "فاکتور خرید", x.Id, "خرید #" + x.Number, x.Supplier != null ? x.Supplier.SupplierFullName : null, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .ToListAsync(ct);
                allItems.AddRange(MapRaw(rows));
            }

            if (ShouldLoad("SalesReturnInvoice"))
            {
                var rows = await _context.SalesReturnInvoices.AsNoTracking().IgnoreQueryFilters()
                    .Include(x => x.Project)
                    .Where(x => x.TenantId == tenantId && (includeDeleted || !x.IsDeleted))
                    .Select(x => new RawAuditRow("SalesReturnInvoice", "برگشت از فروش", x.Id, "برگشت فروش #" + x.Number, x.Project != null ? x.Project.ProjectTitle : null, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .ToListAsync(ct);
                allItems.AddRange(MapRaw(rows));
            }

            if (ShouldLoad("PurchaseReturnInvoice"))
            {
                var rows = await _context.PurchaseReturnInvoices.AsNoTracking().IgnoreQueryFilters()
                    .Include(x => x.Supplier)
                    .Where(x => x.TenantId == tenantId && (includeDeleted || !x.IsDeleted))
                    .Select(x => new RawAuditRow("PurchaseReturnInvoice", "برگشت از خرید", x.Id, "برگشت خرید #" + x.Number, x.Supplier != null ? x.Supplier.SupplierFullName : null, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .ToListAsync(ct);
                allItems.AddRange(MapRaw(rows));
            }

            if (ShouldLoad("Receipt"))
            {
                var rows = await _context.Receipts.AsNoTracking().IgnoreQueryFilters()
                    .Include(x => x.Customer)
                    .Where(x => x.TenantId == tenantId && (includeDeleted || !x.IsDeleted))
                    .Select(x => new RawAuditRow("Receipt", "دریافت وجه", x.Id, $"دریافت #{x.Id} ({x.Amount:N0} ریال)", x.Customer != null ? x.Customer.Name : x.ReferenceNo, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .ToListAsync(ct);
                allItems.AddRange(MapRaw(rows));
            }

            if (ShouldLoad("Payment"))
            {
                var rows = await _context.Payments.AsNoTracking().IgnoreQueryFilters()
                    .Where(x => x.TenantId == tenantId && (includeDeleted || !x.IsDeleted))
                    .Select(x => new RawAuditRow("Payment", "پرداخت وجه", x.Id, $"پرداخت #{x.Id} ({x.Amount:N0} ریال)", x.ReferenceNo ?? x.Method, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .ToListAsync(ct);
                allItems.AddRange(MapRaw(rows));
            }

            if (ShouldLoad("Expense"))
            {
                var rows = await _context.Expenses.AsNoTracking().IgnoreQueryFilters()
                    .Include(x => x.Project)
                    .Where(x => x.TenantId == tenantId && (includeDeleted || !x.IsDeleted))
                    .Select(x => new RawAuditRow("Expense", "هزینه پروژه", x.Id, x.ExpenseTitle, x.Project != null ? x.Project.ProjectTitle : null, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .ToListAsync(ct);
                allItems.AddRange(MapRaw(rows));
            }

            if (ShouldLoad("Cheque"))
            {
                var rows = await _context.Cheques.AsNoTracking().IgnoreQueryFilters()
                    .Where(x => x.TenantId == tenantId && (includeDeleted || !x.IsDeleted))
                    .Select(x => new RawAuditRow("Cheque", "چک صیادی", x.Id, $"چک #{x.ChequeNumber} ({x.Amount:N0} ریال)", x.BankName, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .ToListAsync(ct);
                allItems.AddRange(MapRaw(rows));
            }

            if (ShouldLoad("JournalVoucher"))
            {
                var rows = await _context.JournalVouchers.AsNoTracking().IgnoreQueryFilters()
                    .Where(x => x.TenantId == tenantId && (includeDeleted || !x.IsDeleted))
                    .Select(x => new RawAuditRow("JournalVoucher", "سند حسابداری", x.Id, $"سند #{x.VoucherNumber}", x.Description, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .ToListAsync(ct);
                allItems.AddRange(MapRaw(rows));
            }

            if (ShouldLoad("Project"))
            {
                var rows = await _context.Projects.AsNoTracking().IgnoreQueryFilters()
                    .Include(x => x.Customer)
                    .Where(x => x.TenantId == tenantId && (includeDeleted || !x.IsDeleted))
                    .Select(x => new RawAuditRow("Project", "پروژه", x.Id, x.ProjectTitle, x.Customer != null ? x.Customer.Name : null, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .ToListAsync(ct);
                allItems.AddRange(MapRaw(rows));
            }

            if (ShouldLoad("ProjectMilestone"))
            {
                var rows = await _context.ProjectMilestones.AsNoTracking().IgnoreQueryFilters()
                    .Include(x => x.Project)
                    .Where(x => x.TenantId == tenantId && (includeDeleted || !x.IsDeleted))
                    .Select(x => new RawAuditRow("ProjectMilestone", "مایلستون پروژه", x.Id, x.Title, x.Project != null ? x.Project.ProjectTitle : null, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .ToListAsync(ct);
                allItems.AddRange(MapRaw(rows));
            }

            if (ShouldLoad("ProjectProgressLog"))
            {
                var rows = await _context.ProjectProgressLogs.AsNoTracking().IgnoreQueryFilters()
                    .Include(x => x.Project)
                    .Where(x => x.TenantId == tenantId && (includeDeleted || !x.IsDeleted))
                    .Select(x => new RawAuditRow("ProjectProgressLog", "گزارش پیشرفت", x.Id, x.Title, x.Project != null ? x.Project.ProjectTitle : null, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .ToListAsync(ct);
                allItems.AddRange(MapRaw(rows));
            }

            if (ShouldLoad("ProjectWorkLog"))
            {
                var rows = await _context.ProjectWorkLogs.AsNoTracking().IgnoreQueryFilters()
                    .Include(x => x.Project)
                    .Where(x => x.TenantId == tenantId && (includeDeleted || !x.IsDeleted))
                    .Select(x => new RawAuditRow("ProjectWorkLog", "ساعت کار پروژه", x.Id, $"{x.Hours:0.##} ساعت — {x.Description}", x.Project != null ? x.Project.ProjectTitle : null, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .ToListAsync(ct);
                allItems.AddRange(MapRaw(rows));
            }

            if (ShouldLoad("Customer"))
            {
                var rows = await _context.Customers.AsNoTracking().IgnoreQueryFilters()
                    .Where(x => x.TenantId == tenantId && (includeDeleted || !x.IsDeleted))
                    .Select(x => new RawAuditRow("Customer", "مشتری", x.Id, x.Name, x.Phone, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .ToListAsync(ct);
                allItems.AddRange(MapRaw(rows));
            }

            if (ShouldLoad("Supplier"))
            {
                var rows = await _context.Suppliers.AsNoTracking().IgnoreQueryFilters()
                    .Where(x => x.TenantId == tenantId && (includeDeleted || !x.IsDeleted))
                    .Select(x => new RawAuditRow("Supplier", "تأمین‌کننده", x.Id, x.SupplierFullName, x.SupplierPhone, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .ToListAsync(ct);
                allItems.AddRange(MapRaw(rows));
            }

            if (!string.IsNullOrWhiteSpace(userId))
            {
                var uid = userId.Trim();
                allItems = allItems.Where(i => i.CreatedById == uid || i.ModifiedById == uid).ToList();
            }

            if (fromDate.HasValue)
            {
                var fDate = fromDate.Value.Date;
                allItems = allItems.Where(i => (i.ModifiedAt ?? i.CreatedAt).Date >= fDate).ToList();
            }

            if (toDate.HasValue)
            {
                var tDate = toDate.Value.Date.AddDays(1);
                allItems = allItems.Where(i => (i.ModifiedAt ?? i.CreatedAt) < tDate).ToList();
            }

            await PopulateUserNamesAsync(tenantId, allItems, ct);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var q = search.Trim();
                allItems = allItems.Where(i =>
                    i.DocumentNumberOrTitle.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    i.EntityTitleFa.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (i.ProjectOrPartyTitle != null && i.ProjectOrPartyTitle.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    i.CreatedByName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (i.ModifiedByName != null && i.ModifiedByName.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    i.EntityId.ToString() == q
                ).ToList();
            }

            var ordered = allItems
                .OrderByDescending(i => i.ModifiedAt ?? i.CreatedAt)
                .ThenByDescending(i => i.EntityId)
                .ToList();

            var totalCount = ordered.Count;
            var paged = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new AuditTrailQueryResultDto
            {
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                Items = paged
            };
        }

        public async Task<AuditTrailItemDto?> GetEntityHistoryAsync(
            int tenantId,
            string entityType,
            int entityId,
            CancellationToken ct = default)
        {
            RawAuditRow? row = entityType.Trim() switch
            {
                "SalesInvoice" => await _context.SalesInvoices.AsNoTracking().IgnoreQueryFilters()
                    .Include(x => x.Project)
                    .Where(x => x.TenantId == tenantId && x.Id == entityId)
                    .Select(x => new RawAuditRow("SalesInvoice", "فاکتور فروش", x.Id, "فاکتور #" + x.Number, x.Project != null ? x.Project.ProjectTitle : null, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .FirstOrDefaultAsync(ct),
                "PurchaseInvoice" => await _context.PurchaseInvoices.AsNoTracking().IgnoreQueryFilters()
                    .Include(x => x.Supplier)
                    .Where(x => x.TenantId == tenantId && x.Id == entityId)
                    .Select(x => new RawAuditRow("PurchaseInvoice", "فاکتور خرید", x.Id, "خرید #" + x.Number, x.Supplier != null ? x.Supplier.SupplierFullName : null, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .FirstOrDefaultAsync(ct),
                "SalesReturnInvoice" => await _context.SalesReturnInvoices.AsNoTracking().IgnoreQueryFilters()
                    .Include(x => x.Project)
                    .Where(x => x.TenantId == tenantId && x.Id == entityId)
                    .Select(x => new RawAuditRow("SalesReturnInvoice", "برگشت از فروش", x.Id, "برگشت فروش #" + x.Number, x.Project != null ? x.Project.ProjectTitle : null, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .FirstOrDefaultAsync(ct),
                "PurchaseReturnInvoice" => await _context.PurchaseReturnInvoices.AsNoTracking().IgnoreQueryFilters()
                    .Include(x => x.Supplier)
                    .Where(x => x.TenantId == tenantId && x.Id == entityId)
                    .Select(x => new RawAuditRow("PurchaseReturnInvoice", "برگشت از خرید", x.Id, "برگشت خرید #" + x.Number, x.Supplier != null ? x.Supplier.SupplierFullName : null, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .FirstOrDefaultAsync(ct),
                "Receipt" => await _context.Receipts.AsNoTracking().IgnoreQueryFilters()
                    .Include(x => x.Customer)
                    .Where(x => x.TenantId == tenantId && x.Id == entityId)
                    .Select(x => new RawAuditRow("Receipt", "دریافت وجه", x.Id, $"دریافت #{x.Id} ({x.Amount:N0} ریال)", x.Customer != null ? x.Customer.Name : x.ReferenceNo, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .FirstOrDefaultAsync(ct),
                "Payment" => await _context.Payments.AsNoTracking().IgnoreQueryFilters()
                    .Where(x => x.TenantId == tenantId && x.Id == entityId)
                    .Select(x => new RawAuditRow("Payment", "پرداخت وجه", x.Id, $"پرداخت #{x.Id} ({x.Amount:N0} ریال)", x.ReferenceNo ?? x.Method, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .FirstOrDefaultAsync(ct),
                "Expense" => await _context.Expenses.AsNoTracking().IgnoreQueryFilters()
                    .Include(x => x.Project)
                    .Where(x => x.TenantId == tenantId && x.Id == entityId)
                    .Select(x => new RawAuditRow("Expense", "هزینه پروژه", x.Id, x.ExpenseTitle, x.Project != null ? x.Project.ProjectTitle : null, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .FirstOrDefaultAsync(ct),
                "Cheque" => await _context.Cheques.AsNoTracking().IgnoreQueryFilters()
                    .Where(x => x.TenantId == tenantId && x.Id == entityId)
                    .Select(x => new RawAuditRow("Cheque", "چک صیادی", x.Id, $"چک #{x.ChequeNumber} ({x.Amount:N0} ریال)", x.BankName, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .FirstOrDefaultAsync(ct),
                "JournalVoucher" => await _context.JournalVouchers.AsNoTracking().IgnoreQueryFilters()
                    .Where(x => x.TenantId == tenantId && x.Id == entityId)
                    .Select(x => new RawAuditRow("JournalVoucher", "سند حسابداری", x.Id, $"سند #{x.VoucherNumber}", x.Description, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .FirstOrDefaultAsync(ct),
                "Project" => await _context.Projects.AsNoTracking().IgnoreQueryFilters()
                    .Include(x => x.Customer)
                    .Where(x => x.TenantId == tenantId && x.Id == entityId)
                    .Select(x => new RawAuditRow("Project", "پروژه", x.Id, x.ProjectTitle, x.Customer != null ? x.Customer.Name : null, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .FirstOrDefaultAsync(ct),
                "ProjectMilestone" => await _context.ProjectMilestones.AsNoTracking().IgnoreQueryFilters()
                    .Include(x => x.Project)
                    .Where(x => x.TenantId == tenantId && x.Id == entityId)
                    .Select(x => new RawAuditRow("ProjectMilestone", "مایلستون پروژه", x.Id, x.Title, x.Project != null ? x.Project.ProjectTitle : null, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .FirstOrDefaultAsync(ct),
                "ProjectProgressLog" => await _context.ProjectProgressLogs.AsNoTracking().IgnoreQueryFilters()
                    .Include(x => x.Project)
                    .Where(x => x.TenantId == tenantId && x.Id == entityId)
                    .Select(x => new RawAuditRow("ProjectProgressLog", "گزارش پیشرفت", x.Id, x.Title, x.Project != null ? x.Project.ProjectTitle : null, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .FirstOrDefaultAsync(ct),
                "ProjectWorkLog" => await _context.ProjectWorkLogs.AsNoTracking().IgnoreQueryFilters()
                    .Include(x => x.Project)
                    .Where(x => x.TenantId == tenantId && x.Id == entityId)
                    .Select(x => new RawAuditRow("ProjectWorkLog", "ساعت کار پروژه", x.Id, $"{x.Hours:0.##} ساعت — {x.Description}", x.Project != null ? x.Project.ProjectTitle : null, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .FirstOrDefaultAsync(ct),
                "Customer" => await _context.Customers.AsNoTracking().IgnoreQueryFilters()
                    .Where(x => x.TenantId == tenantId && x.Id == entityId)
                    .Select(x => new RawAuditRow("Customer", "مشتری", x.Id, x.Name, x.Phone, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .FirstOrDefaultAsync(ct),
                "Supplier" => await _context.Suppliers.AsNoTracking().IgnoreQueryFilters()
                    .Where(x => x.TenantId == tenantId && x.Id == entityId)
                    .Select(x => new RawAuditRow("Supplier", "تأمین‌کننده", x.Id, x.SupplierFullName, x.SupplierPhone, x.CreatedAt, x.CreatedById, x.ModifiedAt, x.ModifiedById, x.IsDeleted, x.DeletedAt))
                    .FirstOrDefaultAsync(ct),
                _ => null
            };

            if (row == null) return null;
            var items = MapRaw(new[] { row }).ToList();
            await PopulateUserNamesAsync(tenantId, items, ct);
            return items.FirstOrDefault();
        }

        private async Task PopulateUserNamesAsync(int tenantId, List<AuditTrailItemDto> items, CancellationToken ct)
        {
            var userIds = items
                .SelectMany(i => new[] { i.CreatedById, i.ModifiedById })
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            var userMap = userIds.Count > 0
                ? await _context.Users
                    .AsNoTracking()
                    .Where(u => userIds.Contains(u.Id) || u.TenantId == tenantId)
                    .ToDictionaryAsync(
                        u => u.Id,
                        u => string.IsNullOrWhiteSpace($"{u.UserFirstName} {u.UserLastName}".Trim())
                            ? (u.UserName ?? u.Id)
                            : $"{u.UserFirstName} {u.UserLastName}".Trim(),
                        ct)
                : new Dictionary<string, string>();

            foreach (var item in items)
            {
                item.CreatedByName = !string.IsNullOrWhiteSpace(item.CreatedById) && userMap.TryGetValue(item.CreatedById, out var cName)
                    ? cName
                    : "سیستم";

                if (!string.IsNullOrWhiteSpace(item.ModifiedById))
                {
                    item.ModifiedByName = userMap.TryGetValue(item.ModifiedById, out var mName)
                        ? mName
                        : "سیستم";
                }
            }
        }

        private static IEnumerable<AuditTrailItemDto> MapRaw(IEnumerable<RawAuditRow> rows) => rows.Select(r => new AuditTrailItemDto
        {
            EntityType = r.EntityType,
            EntityTitleFa = r.EntityTitleFa,
            EntityId = r.EntityId,
            DocumentNumberOrTitle = r.DocumentNumberOrTitle,
            ProjectOrPartyTitle = r.ProjectOrPartyTitle,
            CreatedAt = r.CreatedAt,
            CreatedAtShamsi = ConvertDate.ConvertMiladiToShamsi(r.CreatedAt, "yyyy/MM/dd HH:mm"),
            CreatedById = r.CreatedById,
            ModifiedAt = r.ModifiedAt,
            ModifiedAtShamsi = r.ModifiedAt.HasValue ? ConvertDate.ConvertMiladiToShamsi(r.ModifiedAt.Value, "yyyy/MM/dd HH:mm") : null,
            ModifiedById = r.ModifiedById,
            IsDeleted = r.IsDeleted,
            DeletedAt = r.DeletedAt,
            DeletedAtShamsi = r.DeletedAt.HasValue ? ConvertDate.ConvertMiladiToShamsi(r.DeletedAt.Value, "yyyy/MM/dd HH:mm") : null
        });

        private sealed record RawAuditRow(
            string EntityType,
            string EntityTitleFa,
            int EntityId,
            string DocumentNumberOrTitle,
            string? ProjectOrPartyTitle,
            DateTime CreatedAt,
            string? CreatedById,
            DateTime? ModifiedAt,
            string? ModifiedById,
            bool IsDeleted,
            DateTime? DeletedAt);
    }
}
