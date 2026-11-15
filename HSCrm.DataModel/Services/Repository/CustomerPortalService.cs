using System.Globalization;
using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SinaPharm.Common;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.DataModel.Services.Repository
{
    public class CustomerPortalService : ICustomerPortalService
    {
        private const string ProgressAttachmentEntityName = "ProjectProgressLog";

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAccountingService? _accountingService;
        private readonly IPaymentSettlementService? _settlementService;

        public CustomerPortalService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAccountingService? accountingService = null,
            IPaymentSettlementService? settlementService = null)
        {
            _context = context;
            _userManager = userManager;
            _accountingService = accountingService;
            _settlementService = settlementService;
        }

        private async Task<(Customer Customer, ApplicationUser User, Tenant? Tenant)> ResolveCustomerAsync(int tenantId, string userId, CancellationToken ct)
        {
            var customer = await _context.Customers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ApplicationUserId == userId && !c.IsDeleted, ct)
                ?? throw new UnauthorizedAccessException("حساب مشتری متصل به این کاربر یافت نشد.");

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, ct)
                ?? throw new UnauthorizedAccessException("اطلاعات کاربری مشتری یافت نشد.");

            var tenant = await _context.Tenants
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == tenantId && !t.IsDeleted, ct);

            return (customer, user, tenant);
        }

        public async Task<CustomerPortalDashboardDto> GetDashboardAsync(int tenantId, string userId, CancellationToken ct = default)
        {
            var (customer, _, tenant) = await ResolveCustomerAsync(tenantId, userId, ct);

            var projects = await _context.Projects
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(p => p.TenantId == tenantId && p.CustomerId == customer.Id && !p.IsDeleted)
                .OrderByDescending(p => p.Id)
                .ToListAsync(ct);

            var projectIds = projects.Select(p => p.Id).ToList();

            var milestones = projectIds.Count > 0
                ? await _context.ProjectMilestones
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(m => m.TenantId == tenantId && !m.IsDeleted && projectIds.Contains(m.ProjectId))
                    .ToListAsync(ct)
                : new List<ProjectMilestone>();

            var logs = projectIds.Count > 0
                ? await _context.ProjectProgressLogs
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Include(l => l.Milestone)
                    .Include(l => l.RecordedByUser)
                    .Include(l => l.Project)
                    .Where(l => l.TenantId == tenantId && !l.IsDeleted && projectIds.Contains(l.ProjectId))
                    .OrderByDescending(l => l.LogDate)
                    .ThenByDescending(l => l.Id)
                    .ToListAsync(ct)
                : new List<ProjectProgressLog>();

            var recentLogIds = logs.Take(5).Select(l => l.Id).ToList();
            var recentAttachments = recentLogIds.Count > 0
                ? await _context.Attachments
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.EntityName == ProgressAttachmentEntityName && recentLogIds.Contains(a.EntityId))
                    .ToListAsync(ct)
                : new List<Attachment>();

            var invoices = projectIds.Count > 0
                ? await _context.SalesInvoices
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(i => i.TenantId == tenantId && !i.IsDeleted && projectIds.Contains(i.ProjectId) && i.Status != InvoiceStatus.Cancelled)
                    .ToListAsync(ct)
                : new List<SalesInvoice>();

            decimal netBalance = 0m;
            decimal pendingCheques = 0m;
            if (_accountingService != null)
            {
                var st = await _accountingService.GetPartyStatementAsync(AccountingPartyType.Customer, customer.Id, null, null, true, ct);
                netBalance = st.FinalBookBalance;
                pendingCheques = st.PendingChequesAmount;
            }
            else
            {
                var totalInv = invoices.Sum(i => i.Total);
                var totalReceipts = await _context.Receipts
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(r => r.TenantId == tenantId && !r.IsDeleted && (r.CustomerId == customer.Id || (r.SalesInvoice != null && r.SalesInvoice.Project.CustomerId == customer.Id)))
                    .SumAsync(r => (decimal?)r.Amount, ct) ?? 0m;
                netBalance = totalInv - totalReceipts;
            }

            var projectDtos = projects.Select(p =>
            {
                var pm = milestones.Where(m => m.ProjectId == p.Id).ToList();
                var plCount = logs.Count(l => l.ProjectId == p.Id);
                return new CustomerPortalProjectSummaryDto
                {
                    Id = p.Id,
                    ProjectTitle = p.ProjectTitle,
                    ProjectLocation = p.ProjectLocation,
                    Status = p.Status,
                    StatusTitle = TranslateProjectStatus(p.Status),
                    ProgressPercent = p.ProgressPercent,
                    MilestonesCount = pm.Count,
                    CompletedMilestonesCount = pm.Count(m => m.Status == MilestoneStatus.Completed),
                    ProgressLogsCount = plCount,
                    CreatedAt = p.CreatedAt,
                    CreatedAtShamsi = ConvertDate.ConvertMiladiToShamsi(p.CreatedAt, "yyyy/MM/dd")
                };
            }).ToList();

            var recentLogDtos = logs.Take(5).Select(l => new CustomerPortalProgressItemDto
            {
                Id = l.Id,
                ProjectId = l.ProjectId,
                ProjectTitle = l.Project?.ProjectTitle ?? string.Empty,
                MilestoneId = l.MilestoneId,
                MilestoneTitle = l.Milestone?.Title,
                LogDate = l.LogDate,
                LogDateShamsi = ConvertDate.ConvertMiladiToShamsi(l.LogDate, "yyyy/MM/dd"),
                Title = l.Title,
                ReportText = l.ReportText,
                ProgressPercent = l.ProgressPercent,
                WorkshopArea = l.WorkshopArea,
                RecordedByName = l.RecordedByUser != null ? $"{l.RecordedByUser.UserFirstName} {l.RecordedByUser.UserLastName}".Trim() : "تیم اجرایی",
                Attachments = recentAttachments
                    .Where(a => a.EntityId == l.Id)
                    .Select(a => new ProjectAttachmentDto
                    {
                        Id = a.Id,
                        FileName = a.FileName,
                        ContentType = a.ContentType,
                        StoragePath = a.StoragePath,
                        IsImage = a.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase),
                        ProgressLogId = l.Id,
                        ProgressLogTitle = l.Title,
                        LogDateShamsi = ConvertDate.ConvertMiladiToShamsi(l.LogDate, "yyyy/MM/dd"),
                        CreatedAt = a.CreatedAt
                    }).ToList()
            }).ToList();

            var activeCount = projects.Count(p => string.Equals(p.Status, "Active", StringComparison.OrdinalIgnoreCase));
            var completedCount = projects.Count(p => string.Equals(p.Status, "Completed", StringComparison.OrdinalIgnoreCase));

            return new CustomerPortalDashboardDto
            {
                CustomerId = customer.Id,
                CustomerName = customer.Name,
                CompanyTitle = tenant?.TenantTitle,
                TotalProjectsCount = projects.Count,
                ActiveProjectsCount = activeCount,
                CompletedProjectsCount = completedCount,
                AverageProgressPercent = projects.Count > 0 ? Math.Round(projects.Average(p => p.ProgressPercent), 1) : 0m,
                InvoicesCount = invoices.Count,
                TotalInvoicesAmount = invoices.Sum(i => i.Total),
                NetOpenBalance = netBalance,
                PendingChequesAmount = pendingCheques,
                RecentProjects = projectDtos.Take(5).ToList(),
                RecentProgressLogs = recentLogDtos
            };
        }

        public async Task<List<CustomerPortalProjectSummaryDto>> GetProjectsAsync(int tenantId, string userId, CancellationToken ct = default)
        {
            var (customer, _, _) = await ResolveCustomerAsync(tenantId, userId, ct);

            var projects = await _context.Projects
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(p => p.TenantId == tenantId && p.CustomerId == customer.Id && !p.IsDeleted)
                .OrderByDescending(p => p.Id)
                .ToListAsync(ct);

            var projectIds = projects.Select(p => p.Id).ToList();

            var milestones = projectIds.Count > 0
                ? await _context.ProjectMilestones
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(m => m.TenantId == tenantId && !m.IsDeleted && projectIds.Contains(m.ProjectId))
                    .ToListAsync(ct)
                : new List<ProjectMilestone>();

            var logCounts = projectIds.Count > 0
                ? await _context.ProjectProgressLogs
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(l => l.TenantId == tenantId && !l.IsDeleted && projectIds.Contains(l.ProjectId))
                    .GroupBy(l => l.ProjectId)
                    .Select(g => new { ProjectId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.ProjectId, x => x.Count, ct)
                : new Dictionary<int, int>();

            return projects.Select(p =>
            {
                var pm = milestones.Where(m => m.ProjectId == p.Id).ToList();
                return new CustomerPortalProjectSummaryDto
                {
                    Id = p.Id,
                    ProjectTitle = p.ProjectTitle,
                    ProjectLocation = p.ProjectLocation,
                    Status = p.Status,
                    StatusTitle = TranslateProjectStatus(p.Status),
                    ProgressPercent = p.ProgressPercent,
                    MilestonesCount = pm.Count,
                    CompletedMilestonesCount = pm.Count(m => m.Status == MilestoneStatus.Completed),
                    ProgressLogsCount = logCounts.TryGetValue(p.Id, out var c) ? c : 0,
                    CreatedAt = p.CreatedAt,
                    CreatedAtShamsi = ConvertDate.ConvertMiladiToShamsi(p.CreatedAt, "yyyy/MM/dd")
                };
            }).ToList();
        }

        public async Task<CustomerPortalProjectDetailDto> GetProjectDetailAsync(int tenantId, string userId, int projectId, CancellationToken ct = default)
        {
            var (customer, _, _) = await ResolveCustomerAsync(tenantId, userId, ct);

            var project = await _context.Projects
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == tenantId && p.CustomerId == customer.Id && !p.IsDeleted, ct)
                ?? throw new KeyNotFoundException("پروژه مورد نظر برای حساب شما یافت نشد.");

            var milestones = await _context.ProjectMilestones
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(m => m.TenantId == tenantId && m.ProjectId == project.Id && !m.IsDeleted)
                .OrderBy(m => m.SortOrder)
                .ThenBy(m => m.Id)
                .ToListAsync(ct);

            var logs = await _context.ProjectProgressLogs
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Include(l => l.Milestone)
                .Include(l => l.RecordedByUser)
                .Where(l => l.TenantId == tenantId && l.ProjectId == project.Id && !l.IsDeleted)
                .OrderByDescending(l => l.LogDate)
                .ThenByDescending(l => l.Id)
                .ToListAsync(ct);

            var logIds = logs.Select(l => l.Id).ToList();
            var attachments = logIds.Count > 0
                ? await _context.Attachments
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.EntityName == ProgressAttachmentEntityName && logIds.Contains(a.EntityId))
                    .OrderBy(a => a.Id)
                    .ToListAsync(ct)
                : new List<Attachment>();

            return new CustomerPortalProjectDetailDto
            {
                Id = project.Id,
                ProjectTitle = project.ProjectTitle,
                ProjectLocation = project.ProjectLocation,
                Status = project.Status,
                StatusTitle = TranslateProjectStatus(project.Status),
                ProgressPercent = project.ProgressPercent,
                CreatedAtShamsi = ConvertDate.ConvertMiladiToShamsi(project.CreatedAt, "yyyy/MM/dd"),
                Milestones = milestones.Select(m => new CustomerPortalMilestoneDto
                {
                    Id = m.Id,
                    Title = m.Title,
                    Description = m.Description,
                    SortOrder = m.SortOrder,
                    WeightPercent = m.WeightPercent,
                    ProgressPercent = m.ProgressPercent,
                    Status = m.Status,
                    StatusTitle = TranslateMilestoneStatus(m.Status),
                    DueDateShamsi = m.DueDate.HasValue ? ConvertDate.ConvertMiladiToShamsi(m.DueDate.Value, "yyyy/MM/dd") : null,
                    CompletedAtShamsi = m.CompletedAt.HasValue ? ConvertDate.ConvertMiladiToShamsi(m.CompletedAt.Value, "yyyy/MM/dd") : null
                }).ToList(),
                ProgressLogs = logs.Select(l => new CustomerPortalProgressItemDto
                {
                    Id = l.Id,
                    ProjectId = l.ProjectId,
                    ProjectTitle = project.ProjectTitle,
                    MilestoneId = l.MilestoneId,
                    MilestoneTitle = l.Milestone?.Title,
                    LogDate = l.LogDate,
                    LogDateShamsi = ConvertDate.ConvertMiladiToShamsi(l.LogDate, "yyyy/MM/dd"),
                    Title = l.Title,
                    ReportText = l.ReportText,
                    ProgressPercent = l.ProgressPercent,
                    WorkshopArea = l.WorkshopArea,
                    RecordedByName = l.RecordedByUser != null ? $"{l.RecordedByUser.UserFirstName} {l.RecordedByUser.UserLastName}".Trim() : "تیم اجرایی",
                    Attachments = attachments
                        .Where(a => a.EntityId == l.Id)
                        .Select(a => new ProjectAttachmentDto
                        {
                            Id = a.Id,
                            FileName = a.FileName,
                            ContentType = a.ContentType,
                            StoragePath = a.StoragePath,
                            IsImage = a.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase),
                            ProgressLogId = l.Id,
                            ProgressLogTitle = l.Title,
                            LogDateShamsi = ConvertDate.ConvertMiladiToShamsi(l.LogDate, "yyyy/MM/dd"),
                            CreatedAt = a.CreatedAt
                        }).ToList()
                }).ToList()
            };
        }

        public async Task<List<CustomerPortalInvoiceDto>> GetInvoicesAsync(int tenantId, string userId, CancellationToken ct = default)
        {
            var (customer, _, tenant) = await ResolveCustomerAsync(tenantId, userId, ct);

            var invoices = await _context.SalesInvoices
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Include(s => s.Project)
                .Include(s => s.Items)
                    .ThenInclude(i => i.Product)
                .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.Project != null && s.Project.CustomerId == customer.Id)
                .OrderByDescending(s => s.IssueDate)
                .ThenByDescending(s => s.Id)
                .ToListAsync(ct);

            var companyInfo = BuildCompanyPrintInfo(tenant);
            return invoices.Select(inv => MapInvoiceDto(inv, customer, companyInfo)).ToList();
        }

        public async Task<CustomerPortalInvoiceDto> GetInvoiceByIdAsync(int tenantId, string userId, int invoiceId, CancellationToken ct = default)
        {
            var (customer, _, tenant) = await ResolveCustomerAsync(tenantId, userId, ct);

            var inv = await _context.SalesInvoices
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Include(s => s.Project)
                .Include(s => s.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(s => s.Id == invoiceId && s.TenantId == tenantId && !s.IsDeleted && s.Project != null && s.Project.CustomerId == customer.Id, ct)
                ?? throw new KeyNotFoundException("فاکتور فروش مورد نظر یافت نشد یا متعلق به شما نیست.");

            return MapInvoiceDto(inv, customer, BuildCompanyPrintInfo(tenant));
        }

        public async Task<CustomerPortalStatementDto> GetStatementAsync(
            int tenantId,
            string userId,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? fiscalYearId = null,
            CancellationToken ct = default)
        {
            var (customer, _, _) = await ResolveCustomerAsync(tenantId, userId, ct);

            PartyStatementReportDto? statement = null;
            if (_accountingService != null)
            {
                statement = await _accountingService.GetPartyStatementAsync(
                    AccountingPartyType.Customer,
                    customer.Id,
                    fromDate,
                    toDate,
                    true,
                    ct);
            }

            var receiptsQuery = _context.Receipts
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Include(r => r.BankAccount)
                .Include(r => r.SalesInvoice)
                    .ThenInclude(si => si!.Project)
                .Where(r => r.TenantId == tenantId && !r.IsDeleted &&
                            (r.CustomerId == customer.Id || (r.SalesInvoice != null && r.SalesInvoice.Project != null && r.SalesInvoice.Project.CustomerId == customer.Id)));

            if (fiscalYearId.HasValue && fiscalYearId.Value > 0)
                receiptsQuery = receiptsQuery.Where(r => r.FiscalYearId == fiscalYearId.Value);
            if (fromDate.HasValue)
                receiptsQuery = receiptsQuery.Where(r => r.ReceiptDate >= fromDate.Value);
            if (toDate.HasValue)
                receiptsQuery = receiptsQuery.Where(r => r.ReceiptDate <= toDate.Value);

            var receipts = await receiptsQuery
                .OrderByDescending(r => r.ReceiptDate)
                .ThenByDescending(r => r.Id)
                .ToListAsync(ct);

            return new CustomerPortalStatementDto
            {
                Statement = statement,
                Receipts = receipts.Select(r => new CustomerPortalReceiptDto
                {
                    Id = r.Id,
                    ReceiptDate = r.ReceiptDate,
                    ReceiptDateShamsi = ConvertDate.ConvertMiladiToShamsi(r.ReceiptDate, "yyyy/MM/dd"),
                    Amount = r.Amount,
                    Method = r.Method,
                    MethodTitle = TranslatePaymentMethod(r.Method),
                    ReferenceNo = r.ReferenceNo,
                    BankAccountTitle = r.BankAccount != null ? $"{r.BankAccount.Title} - {r.BankAccount.BankName}" : null,
                    SalesInvoiceId = r.SalesInvoiceId,
                    InvoiceNumber = r.SalesInvoice?.Number,
                    Description = r.Description
                }).ToList()
            };
        }

        public async Task<CustomerPortalProfileDto> GetProfileAsync(int tenantId, string userId, CancellationToken ct = default)
        {
            var (customer, user, tenant) = await ResolveCustomerAsync(tenantId, userId, ct);
            return new CustomerPortalProfileDto
            {
                CustomerId = customer.Id,
                UserId = user.Id,
                UserName = user.UserName ?? string.Empty,
                UserFirstName = user.UserFirstName,
                UserLastName = user.UserLastName,
                FullName = customer.Name,
                Phone = customer.Phone ?? user.PhoneNumber,
                Email = customer.Email ?? user.Email,
                Address = customer.Address,
                UserImageUrl = user.UserImageUrl,
                CompanyTitle = tenant?.TenantTitle
            };
        }

        public async Task<CustomerPortalProfileDto> UpdateProfileAsync(int tenantId, string userId, CustomerPortalProfileUpdateModel model, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(model.UserFirstName) || string.IsNullOrWhiteSpace(model.UserLastName))
                throw new ArgumentException("نام و نام خانوادگی نمی‌تواند خالی باشد.");

            var (customer, user, tenant) = await ResolveCustomerAsync(tenantId, userId, ct);

            var cleanEmail = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
            if (!string.IsNullOrWhiteSpace(cleanEmail) && !string.Equals(cleanEmail, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                var existingByEmail = await _userManager.FindByEmailAsync(cleanEmail);
                if (existingByEmail != null && existingByEmail.Id != user.Id)
                    throw new InvalidOperationException("این ایمیل قبلاً برای کاربر دیگری ثبت شده است.");
            }

            user.UserFirstName = model.UserFirstName.Trim();
            user.UserLastName = model.UserLastName.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
            user.Email = cleanEmail;

            var identityResult = await _userManager.UpdateAsync(user);
            if (!identityResult.Succeeded)
                throw new InvalidOperationException(string.Join(" | ", identityResult.Errors.Select(e => e.Description)));

            customer.Name = $"{user.UserFirstName} {user.UserLastName}".Trim();
            customer.Phone = user.PhoneNumber;
            customer.Email = cleanEmail;
            customer.Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim();
            customer.ModifiedAt = DateTime.UtcNow;
            customer.ModifiedById = userId;

            await _context.SaveChangesAsync(ct);

            return new CustomerPortalProfileDto
            {
                CustomerId = customer.Id,
                UserId = user.Id,
                UserName = user.UserName ?? string.Empty,
                UserFirstName = user.UserFirstName,
                UserLastName = user.UserLastName,
                FullName = customer.Name,
                Phone = customer.Phone,
                Email = customer.Email,
                Address = customer.Address,
                UserImageUrl = user.UserImageUrl,
                CompanyTitle = tenant?.TenantTitle
            };
        }

        public async Task<(bool Succeeded, string Message)> ChangePasswordAsync(int tenantId, string userId, CustomerPortalChangePasswordModel model, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(model.CurrentPassword) || string.IsNullOrWhiteSpace(model.NewPassword))
                return (false, "رمز عبور فعلی و رمز عبور جدید الزامی هستند.");

            if (!string.Equals(model.NewPassword, model.ConfirmPassword, StringComparison.Ordinal))
                return (false, "تکرار رمز عبور جدید با رمز عبور جدید مطابقت ندارد.");

            var (_, user, _) = await ResolveCustomerAsync(tenantId, userId, ct);
            var res = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (!res.Succeeded)
                return (false, string.Join(" | ", res.Errors.Select(e => e.Description)));

            return (true, "رمز عبور شما با موفقیت تغییر یافت.");
        }

        private CustomerPortalInvoiceDto MapInvoiceDto(SalesInvoice inv, Customer customer, CustomerPortalCompanyPrintInfoDto companyInfo)
        {
            var paid = 0m;
            var remaining = inv.Total;
            if (_settlementService != null)
            {
                var summary = _settlementService.BuildSalesInvoiceSummary(inv.Id, inv.Total);
                paid = summary.PaidAmount;
                remaining = summary.RemainingAmount;
            }

            return new CustomerPortalInvoiceDto
            {
                Id = inv.Id,
                Number = inv.Number,
                ProjectId = inv.ProjectId,
                ProjectTitle = inv.Project?.ProjectTitle ?? string.Empty,
                CustomerName = customer.Name,
                CustomerPhone = customer.Phone,
                CustomerAddress = customer.Address,
                IssueDate = inv.IssueDate,
                IssueDateShamsi = ConvertDate.ConvertMiladiToShamsi(inv.IssueDate, "yyyy/MM/dd"),
                Status = inv.Status,
                StatusTitle = TranslateInvoiceStatus(inv.Status),
                Subtotal = inv.Subtotal,
                Tax = inv.Tax,
                Discount = inv.Discount,
                Total = inv.Total,
                PaidAmount = paid,
                RemainingAmount = remaining,
                Description = inv.Description,
                CompanyInfo = companyInfo,
                Items = (inv.Items ?? new List<SalesInvoiceItem>()).Select(it => new CustomerPortalInvoiceLineDto
                {
                    Id = it.Id,
                    ProductId = it.ProductId,
                    ProductTitle = it.Product?.ProductTitle ?? $"کالا #{it.ProductId}",
                    Unit = it.Product?.ProductUnit,
                    Quantity = it.Quantity,
                    UnitPrice = it.UnitPriceSell,
                    Discount = it.Discount ?? 0m,
                    Tax = it.Tax ?? 0m,
                    Total = it.LineTotal
                }).ToList()
            };
        }

        private static CustomerPortalCompanyPrintInfoDto BuildCompanyPrintInfo(Tenant? tenant) => new()
        {
            CompanyTitle = tenant?.TenantTitle ?? "شرکت",
            LogoUrl = tenant?.LogoUrl,
            TaxId = tenant?.TaxId,
            CompanyPhone = tenant?.CompanyPhone,
            CompanyEmail = tenant?.CompanyEmail,
            Address = tenant?.Address,
            InvoiceFooterText = tenant?.InvoiceFooterText,
            Currency = string.IsNullOrWhiteSpace(tenant?.Currency) ? "IRR" : tenant!.Currency
        };

        private static string TranslateProjectStatus(string? status) => status?.ToLowerInvariant() switch
        {
            "active" => "در حال اجرا",
            "completed" => "تکمیل‌شده",
            "onhold" => "متوقف موقت",
            "cancelled" => "لغوشده",
            _ => status ?? "نامشخص"
        };

        private static string TranslateMilestoneStatus(MilestoneStatus status) => status switch
        {
            MilestoneStatus.NotStarted => "شروع نشده",
            MilestoneStatus.InProgress => "در حال انجام",
            MilestoneStatus.Completed => "تکمیل شده",
            MilestoneStatus.Delayed => "دارای تأخیر",
            _ => "نامشخص"
        };

        private static string TranslateInvoiceStatus(InvoiceStatus status) => status switch
        {
            InvoiceStatus.Proforma => "پیش‌فاکتور",
            InvoiceStatus.Final => "قطعی شده",
            InvoiceStatus.Cancelled => "لغو شده",
            InvoiceStatus.Rejected => "رد شده",
            _ => "نامشخص"
        };

        private static string TranslatePaymentMethod(string? method) => method?.ToLowerInvariant() switch
        {
            "cash" => "نقدی",
            "card" => "کارت‌خوان / کارت به کارت",
            "transfer" => "حواله بانکی",
            "cheque" => "چک",
            _ => method ?? "نامشخص"
        };
    }
}
