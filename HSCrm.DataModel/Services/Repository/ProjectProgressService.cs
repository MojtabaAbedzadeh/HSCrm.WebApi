using System.Globalization;
using HSCrm.Common.PublicTools;
using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using Microsoft.EntityFrameworkCore;
using SinaPharm.Common;
using static HSCrm.Common.PublicTools.Enums;
using static HSCrm.Common.PublicTools.StringCleaner;

namespace HSCrm.DataModel.Services.Repository
{
    public class ProjectProgressService : IProjectProgressService
    {
        private const string AttachmentEntityName = "ProjectProgressLog";
        private const long MaxAttachmentSizeBytes = 10 * 1024 * 1024; // 10 MB per file

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt"
        };

        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif"
        };

        private readonly ApplicationDbContext _context;
        private readonly IProjectAccessService _projectAccess;

        public ProjectProgressService(ApplicationDbContext context, IProjectAccessService projectAccess)
        {
            _context = context;
            _projectAccess = projectAccess;
        }

        public async Task<List<ProjectMilestoneDto>> GetMilestonesAsync(int tenantId, string actorUserId, int projectId, CancellationToken ct = default)
        {
            await EnsureProjectAccessAsync(tenantId, actorUserId, projectId, ct);
            return await LoadMilestonesInternalAsync(tenantId, projectId, ct);
        }

        public async Task<ProjectMilestoneDto> CreateMilestoneAsync(int tenantId, string actorUserId, ProjectMilestoneCreateModel model, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                throw new ArgumentException("عنوان مرحله (Milestone) الزامی است.");
            if (model.WeightPercent < 0m || model.WeightPercent > 100m)
                throw new ArgumentException("وزن مرحله باید بین ۰ تا ۱۰۰ درصد باشد.");
            if (model.ProgressPercent < 0m || model.ProgressPercent > 100m)
                throw new ArgumentException("درصد پیشرفت مرحله باید بین ۰ تا ۱۰۰ باشد.");

            var project = await _context.Projects
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == model.ProjectId && p.TenantId == tenantId && !p.IsDeleted, ct)
                ?? throw new KeyNotFoundException("پروژه مورد نظر یافت نشد.");

            await EnsureProjectAccessAsync(tenantId, actorUserId, model.ProjectId, ct);

            DateTime? dueDate = string.IsNullOrWhiteSpace(model.DueDate) ? null : ParseDate(model.DueDate);
            var status = DeriveMilestoneStatus(model.ProgressPercent, null);

            var milestone = new ProjectMilestone
            {
                TenantId = tenantId,
                ProjectId = project.Id,
                Title = model.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                SortOrder = model.SortOrder,
                WeightPercent = Math.Round(model.WeightPercent, 2, MidpointRounding.AwayFromZero),
                ProgressPercent = Math.Round(model.ProgressPercent, 2, MidpointRounding.AwayFromZero),
                DueDate = dueDate,
                Status = status,
                CompletedAt = status == MilestoneStatus.Completed ? DateTime.UtcNow : null,
                CreatedAt = DateTime.UtcNow,
                CreatedById = actorUserId,
                IsDeleted = false
            };

            _context.ProjectMilestones.Add(milestone);
            await _context.SaveChangesAsync(ct);

            await RecalculateProjectProgressAsync(tenantId, project.Id, ct);

            return MapMilestoneDto(milestone, project.ProjectTitle, 0);
        }

        public async Task<ProjectMilestoneDto> UpdateMilestoneAsync(int tenantId, string actorUserId, ProjectMilestoneUpdateModel model, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                throw new ArgumentException("عنوان مرحله (Milestone) الزامی است.");
            if (model.WeightPercent < 0m || model.WeightPercent > 100m)
                throw new ArgumentException("وزن مرحله باید بین ۰ تا ۱۰۰ درصد باشد.");
            if (model.ProgressPercent < 0m || model.ProgressPercent > 100m)
                throw new ArgumentException("درصد پیشرفت مرحله باید بین ۰ تا ۱۰۰ باشد.");

            var milestone = await _context.ProjectMilestones
                .IgnoreQueryFilters()
                .Include(m => m.Project)
                .FirstOrDefaultAsync(m => m.Id == model.Id && m.TenantId == tenantId && !m.IsDeleted, ct)
                ?? throw new KeyNotFoundException("مرحله مورد نظر یافت نشد.");

            await EnsureProjectAccessAsync(tenantId, actorUserId, milestone.ProjectId, ct);

            milestone.Title = model.Title.Trim();
            milestone.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
            milestone.SortOrder = model.SortOrder;
            milestone.WeightPercent = Math.Round(model.WeightPercent, 2, MidpointRounding.AwayFromZero);
            milestone.ProgressPercent = Math.Round(model.ProgressPercent, 2, MidpointRounding.AwayFromZero);
            milestone.DueDate = string.IsNullOrWhiteSpace(model.DueDate) ? null : ParseDate(model.DueDate);
            milestone.Status = DeriveMilestoneStatus(milestone.ProgressPercent, model.Status);
            if (milestone.Status == MilestoneStatus.Completed)
                milestone.CompletedAt ??= DateTime.UtcNow;
            else
                milestone.CompletedAt = null;
            milestone.ModifiedAt = DateTime.UtcNow;
            milestone.ModifiedById = actorUserId;

            await _context.SaveChangesAsync(ct);
            await RecalculateProjectProgressAsync(tenantId, milestone.ProjectId, ct);

            var logsCount = await _context.ProjectProgressLogs
                .IgnoreQueryFilters()
                .CountAsync(l => l.TenantId == tenantId && l.MilestoneId == milestone.Id && !l.IsDeleted, ct);

            return MapMilestoneDto(milestone, milestone.Project?.ProjectTitle ?? string.Empty, logsCount);
        }

        public async Task<bool> DeleteMilestoneAsync(int tenantId, string actorUserId, int milestoneId, CancellationToken ct = default)
        {
            var milestone = await _context.ProjectMilestones
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.Id == milestoneId && m.TenantId == tenantId && !m.IsDeleted, ct)
                ?? throw new KeyNotFoundException("مرحله مورد نظر یافت نشد.");

            await EnsureProjectAccessAsync(tenantId, actorUserId, milestone.ProjectId, ct);

            milestone.IsDeleted = true;
            milestone.DeletedAt = DateTime.UtcNow;
            milestone.DeletedById = actorUserId;
            milestone.ModifiedAt = DateTime.UtcNow;
            milestone.ModifiedById = actorUserId;

            await _context.SaveChangesAsync(ct);
            await RecalculateProjectProgressAsync(tenantId, milestone.ProjectId, ct);
            return true;
        }

        public async Task<List<ProjectProgressLogDto>> GetTimelineAsync(int tenantId, string actorUserId, int projectId, int? fiscalYearId = null, CancellationToken ct = default)
        {
            await EnsureProjectAccessAsync(tenantId, actorUserId, projectId, ct);

            var logsQuery = _context.ProjectProgressLogs
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Include(l => l.Project)
                .Include(l => l.Milestone)
                .Include(l => l.RecordedByUser)
                .Where(l => l.TenantId == tenantId && l.ProjectId == projectId && !l.IsDeleted);

            if (fiscalYearId.HasValue && fiscalYearId.Value > 0)
                logsQuery = logsQuery.Where(l => l.FiscalYearId == fiscalYearId.Value);

            var logs = await logsQuery
                .OrderByDescending(l => l.LogDate)
                .ThenByDescending(l => l.Id)
                .ToListAsync(ct);

            if (logs.Count == 0)
                return new List<ProjectProgressLogDto>();

            var logIds = logs.Select(l => l.Id).ToList();
            var attachments = await _context.Attachments
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.EntityName == AttachmentEntityName && logIds.Contains(a.EntityId))
                .OrderBy(a => a.Id)
                .ToListAsync(ct);

            var attachmentsByLog = attachments.GroupBy(a => a.EntityId)
                .ToDictionary(g => g.Key, g => g.ToList());

            return logs.Select(l =>
            {
                var logAtts = attachmentsByLog.TryGetValue(l.Id, out var list) ? list : new List<Attachment>();
                return MapProgressLogDto(l, logAtts);
            }).ToList();
        }

        public async Task<List<ProjectAttachmentDto>> GetGalleryAsync(int tenantId, string actorUserId, int projectId, CancellationToken ct = default)
        {
            await EnsureProjectAccessAsync(tenantId, actorUserId, projectId, ct);

            var logs = await _context.ProjectProgressLogs
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Include(l => l.RecordedByUser)
                .Where(l => l.TenantId == tenantId && l.ProjectId == projectId && !l.IsDeleted)
                .ToListAsync(ct);

            if (logs.Count == 0)
                return new List<ProjectAttachmentDto>();

            var logMap = logs.ToDictionary(l => l.Id);
            var logIds = logMap.Keys.ToList();

            var attachments = await _context.Attachments
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.EntityName == AttachmentEntityName && logIds.Contains(a.EntityId))
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id)
                .ToListAsync(ct);

            return attachments.Select(a =>
            {
                logMap.TryGetValue(a.EntityId, out var parentLog);
                return MapAttachmentDto(a, parentLog);
            }).ToList();
        }

        public async Task<ProjectProgressLogDto> AddProgressLogAsync(
            int tenantId,
            int fiscalYearId,
            string actorUserId,
            ProjectProgressLogCreateInput model,
            string webRootPath,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                throw new ArgumentException("عنوان گزارش پیشرفت الزامی است.");
            if (string.IsNullOrWhiteSpace(model.ReportText))
                throw new ArgumentException("متن گزارش کارگاهی الزامی است.");
            if (model.ProgressPercent < 0m || model.ProgressPercent > 100m)
                throw new ArgumentException("درصد پیشرفت باید بین ۰ تا ۱۰۰ باشد.");

            var project = await _context.Projects
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == model.ProjectId && p.TenantId == tenantId && !p.IsDeleted, ct)
                ?? throw new KeyNotFoundException("پروژه مورد نظر یافت نشد.");

            await EnsureProjectAccessAsync(tenantId, actorUserId, project.Id, ct);

            ProjectMilestone? milestone = null;
            if (model.MilestoneId.HasValue && model.MilestoneId.Value > 0)
            {
                milestone = await _context.ProjectMilestones
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(m => m.Id == model.MilestoneId.Value && m.TenantId == tenantId && m.ProjectId == project.Id && !m.IsDeleted, ct)
                    ?? throw new KeyNotFoundException("مرحله انتخاب‌شده برای این پروژه یافت نشد.");
            }

            if (model.Files != null)
            {
                foreach (var file in model.Files)
                {
                    ValidateUploadFile(file);
                }
            }

            var logDate = string.IsNullOrWhiteSpace(model.LogDate) ? DateTime.UtcNow.Date : ParseDate(model.LogDate);
            var clampedPct = Math.Round(model.ProgressPercent, 2, MidpointRounding.AwayFromZero);

            var log = new ProjectProgressLog
            {
                TenantId = tenantId,
                ProjectId = project.Id,
                MilestoneId = milestone?.Id,
                FiscalYearId = fiscalYearId,
                RecordedByUserId = actorUserId,
                LogDate = logDate,
                Title = model.Title.Trim(),
                ReportText = model.ReportText.Trim(),
                ProgressPercent = clampedPct,
                WorkshopArea = string.IsNullOrWhiteSpace(model.WorkshopArea) ? null : model.WorkshopArea.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedById = actorUserId,
                IsDeleted = false
            };

            _context.ProjectProgressLogs.Add(log);

            if (milestone != null)
            {
                milestone.ProgressPercent = clampedPct;
                milestone.Status = DeriveMilestoneStatus(clampedPct, milestone.Status == MilestoneStatus.Delayed && clampedPct < 100m ? MilestoneStatus.Delayed : null);
                if (milestone.Status == MilestoneStatus.Completed)
                    milestone.CompletedAt ??= DateTime.UtcNow;
                else
                    milestone.CompletedAt = null;
                milestone.ModifiedAt = DateTime.UtcNow;
                milestone.ModifiedById = actorUserId;
            }

            await _context.SaveChangesAsync(ct);

            var createdAttachments = new List<Attachment>();
            if (model.Files != null && model.Files.Count > 0)
            {
                var root = string.IsNullOrWhiteSpace(webRootPath)
                    ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
                    : webRootPath;
                var uploadDir = Path.Combine(root, "upload", "project-progress", tenantId.ToString(CultureInfo.InvariantCulture), project.Id.ToString(CultureInfo.InvariantCulture));
                Directory.CreateDirectory(uploadDir);

                foreach (var file in model.Files)
                {
                    if (file.Length <= 0 || file.Content.Length == 0)
                        continue;

                    var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? string.Empty;
                    var safeName = $"log_{log.Id}_{Guid.NewGuid():N}{ext}";
                    var physicalPath = Path.Combine(uploadDir, safeName);
                    await File.WriteAllBytesAsync(physicalPath, file.Content, ct);

                    var originalName = Path.GetFileName(file.FileName);
                    if (originalName.Length > 250)
                        originalName = originalName[^250..];

                    var attachment = new Attachment
                    {
                        TenantId = tenantId,
                        FileName = originalName,
                        ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                        StoragePath = $"/upload/project-progress/{tenantId}/{project.Id}/{safeName}",
                        EntityName = AttachmentEntityName,
                        EntityId = log.Id,
                        CreatedAt = DateTime.UtcNow,
                        CreatedById = actorUserId,
                        IsDeleted = false
                    };
                    _context.Attachments.Add(attachment);
                    createdAttachments.Add(attachment);
                }

                if (createdAttachments.Count > 0)
                    await _context.SaveChangesAsync(ct);
            }

            await RecalculateProjectProgressAsync(tenantId, project.Id, ct);

            log.Project = project;
            log.Milestone = milestone;
            log.RecordedByUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == actorUserId, ct) ?? null!;

            return MapProgressLogDto(log, createdAttachments);
        }

        public async Task<bool> DeleteProgressLogAsync(int tenantId, string actorUserId, int logId, CancellationToken ct = default)
        {
            var log = await _context.ProjectProgressLogs
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(l => l.Id == logId && l.TenantId == tenantId && !l.IsDeleted, ct)
                ?? throw new KeyNotFoundException("گزارش پیشرفت یافت نشد.");

            await EnsureProjectAccessAsync(tenantId, actorUserId, log.ProjectId, ct);

            var fullAccess = await _projectAccess.HasFullProjectAccessAsync(actorUserId, ct);
            if (!fullAccess && log.RecordedByUserId != actorUserId)
                throw new UnauthorizedAccessException("فقط مجاز به حذف گزارش‌های ثبت‌شده توسط خودتان هستید.");

            log.IsDeleted = true;
            log.DeletedAt = DateTime.UtcNow;
            log.DeletedById = actorUserId;
            log.ModifiedAt = DateTime.UtcNow;
            log.ModifiedById = actorUserId;

            var linkedAttachments = await _context.Attachments
                .IgnoreQueryFilters()
                .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.EntityName == AttachmentEntityName && a.EntityId == log.Id)
                .ToListAsync(ct);

            foreach (var att in linkedAttachments)
            {
                att.IsDeleted = true;
                att.DeletedAt = DateTime.UtcNow;
                att.DeletedById = actorUserId;
                att.ModifiedAt = DateTime.UtcNow;
                att.ModifiedById = actorUserId;
            }

            await _context.SaveChangesAsync(ct);
            await RecalculateProjectProgressAsync(tenantId, log.ProjectId, ct);
            return true;
        }

        public async Task<ProjectProfitAndLossDto> GetProjectProfitAndLossAsync(
            int tenantId,
            string actorUserId,
            int projectId,
            int? fiscalYearId = null,
            CancellationToken ct = default)
        {
            await EnsureProjectAccessAsync(tenantId, actorUserId, projectId, ct);

            var project = await _context.Projects
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Include(p => p.Customer)
                .FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == tenantId && !p.IsDeleted, ct)
                ?? throw new KeyNotFoundException("پروژه مورد نظر یافت نشد.");

            var salesQuery = _context.SalesInvoices
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Include(s => s.Items)
                    .ThenInclude(i => i.Product)
                .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.ProjectId == projectId);

            if (fiscalYearId.HasValue && fiscalYearId.Value > 0)
                salesQuery = salesQuery.Where(s => s.FiscalYearId == fiscalYearId.Value);

            var salesInvoices = await salesQuery
                .OrderByDescending(s => s.IssueDate)
                .ThenByDescending(s => s.Id)
                .ToListAsync(ct);

            var finalSales = salesInvoices.Where(s => s.Status == InvoiceStatus.Final).ToList();
            var proformaSales = salesInvoices.Where(s => s.Status == InvoiceStatus.Proforma).ToList();

            var returnQuery = _context.SalesReturnInvoices
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Include(r => r.Items)
                    .ThenInclude(i => i.Product)
                .Where(r => r.TenantId == tenantId && !r.IsDeleted && r.ProjectId == projectId && r.Status == InvoiceStatus.Final);

            if (fiscalYearId.HasValue && fiscalYearId.Value > 0)
                returnQuery = returnQuery.Where(r => r.FiscalYearId == fiscalYearId.Value);

            var returnInvoices = await returnQuery
                .OrderByDescending(r => r.IssueDate)
                .ThenByDescending(r => r.Id)
                .ToListAsync(ct);

            var expenseQuery = _context.Expenses
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(e => e.TenantId == tenantId && !e.IsDeleted && e.ProjectId == projectId);

            if (fiscalYearId.HasValue && fiscalYearId.Value > 0)
                expenseQuery = expenseQuery.Where(e => e.FiscalYearId == fiscalYearId.Value);

            var expenses = await expenseQuery
                .OrderByDescending(e => e.ExpenseDate)
                .ThenByDescending(e => e.Id)
                .ToListAsync(ct);

            var workLogQuery = _context.ProjectWorkLogs
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Include(w => w.User)
                .Where(w => w.TenantId == tenantId && !w.IsDeleted && w.ProjectId == projectId);

            if (fiscalYearId.HasValue && fiscalYearId.Value > 0)
                workLogQuery = workLogQuery.Where(w => w.FiscalYearId == fiscalYearId.Value);

            var workLogs = await workLogQuery.ToListAsync(ct);

            var memberRoles = await _context.ProjectMembers
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(m => m.TenantId == tenantId && !m.IsDeleted && m.ProjectId == projectId)
                .ToDictionaryAsync(m => m.UserId, m => m.ProjectRole, ct);

            var grossSalesRevenue = finalSales.Sum(s => s.Total);
            var salesReturnsAmount = returnInvoices.Sum(r => r.Total);
            var netSalesRevenue = grossSalesRevenue - salesReturnsAmount;
            var proformaSalesTotal = proformaSales.Sum(s => s.Total);

            var grossMaterialCogs = finalSales
                .SelectMany(s => s.Items.Where(i => !i.IsDeleted))
                .Sum(i => i.TotalCost);

            var returnedMaterialCogs = returnInvoices
                .SelectMany(r => r.Items.Where(i => !i.IsDeleted))
                .Sum(i => i.TotalCost);

            var netMaterialCogs = grossMaterialCogs - returnedMaterialCogs;

            var totalDirectExpenses = expenses.Sum(e => e.Amount);

            var approvedWorkLogs = workLogs.Where(w => w.Status == WorkLogStatus.Approved).ToList();
            var pendingWorkLogs = workLogs.Where(w => w.Status == WorkLogStatus.Pending).ToList();

            var totalApprovedLaborCost = approvedWorkLogs.Sum(w => w.WageCost);
            var totalApprovedHours = approvedWorkLogs.Sum(w => w.Hours);
            var pendingLaborCostEstimate = pendingWorkLogs.Sum(w => w.WageCost);
            var pendingHours = pendingWorkLogs.Sum(w => w.Hours);

            var totalCosts = netMaterialCogs + totalDirectExpenses + totalApprovedLaborCost;
            var grossProfit = netSalesRevenue - netMaterialCogs;
            var netProfit = netSalesRevenue - totalCosts;
            var marginPercent = netSalesRevenue > 0m
                ? Math.Round((netProfit / netSalesRevenue) * 100m, 2, MidpointRounding.AwayFromZero)
                : 0m;

            decimal? budgetVariance = null;
            decimal? budgetUsedPercent = null;
            bool isOverBudget = false;
            if (project.EstimatedBudget.HasValue)
            {
                budgetVariance = project.EstimatedBudget.Value - totalCosts;
                if (project.EstimatedBudget.Value > 0m)
                {
                    budgetUsedPercent = Math.Round((totalCosts / project.EstimatedBudget.Value) * 100m, 2, MidpointRounding.AwayFromZero);
                }
                else
                {
                    budgetUsedPercent = totalCosts > 0m ? 100m : 0m;
                }
                isOverBudget = totalCosts > project.EstimatedBudget.Value;
            }

            var laborByMembers = workLogs
                .Where(w => w.Status != WorkLogStatus.Rejected)
                .GroupBy(w => new
                {
                    w.UserId,
                    Name = w.User != null ? $"{w.User.UserFirstName} {w.User.UserLastName}".Trim() : w.UserId
                })
                .Select(g =>
                {
                    memberRoles.TryGetValue(g.Key.UserId, out var roleTitle);
                    return new ProjectProfitAndLossMemberWageDto
                    {
                        UserId = g.Key.UserId,
                        EmployeeName = g.Key.Name,
                        ProjectRole = roleTitle,
                        ApprovedHours = g.Where(x => x.Status == WorkLogStatus.Approved).Sum(x => x.Hours),
                        ApprovedWageCost = g.Where(x => x.Status == WorkLogStatus.Approved).Sum(x => x.WageCost),
                        PendingHours = g.Where(x => x.Status == WorkLogStatus.Pending).Sum(x => x.Hours),
                        PendingWageCostEstimate = g.Where(x => x.Status == WorkLogStatus.Pending).Sum(x => x.WageCost)
                    };
                })
                .OrderByDescending(m => m.ApprovedWageCost)
                .ThenByDescending(m => m.PendingWageCostEstimate)
                .ToList();

            var soldByProduct = finalSales
                .SelectMany(s => s.Items.Where(i => !i.IsDeleted))
                .GroupBy(i => new
                {
                    i.ProductId,
                    Title = i.Product?.ProductTitle ?? $"کالای #{i.ProductId}",
                    Unit = i.Product?.ProductUnit
                })
                .ToDictionary(g => g.Key.ProductId, g => new
                {
                    g.Key.Title,
                    g.Key.Unit,
                    Qty = g.Sum(x => x.Quantity),
                    LineTotal = g.Sum(x => x.LineTotal),
                    Cogs = g.Sum(x => x.TotalCost)
                });

            var returnedByProduct = returnInvoices
                .SelectMany(r => r.Items.Where(i => !i.IsDeleted))
                .GroupBy(i => new
                {
                    i.ProductId,
                    Title = i.Product?.ProductTitle ?? $"کالای #{i.ProductId}",
                    Unit = i.Product?.ProductUnit
                })
                .ToDictionary(g => g.Key.ProductId, g => new
                {
                    g.Key.Title,
                    g.Key.Unit,
                    Qty = g.Sum(x => x.Quantity),
                    LineTotal = g.Sum(x => x.LineTotal),
                    Cogs = g.Sum(x => x.TotalCost)
                });

            var allProductIds = soldByProduct.Keys.Union(returnedByProduct.Keys).Distinct().ToList();
            var materialItems = allProductIds.Select(pid =>
            {
                soldByProduct.TryGetValue(pid, out var sold);
                returnedByProduct.TryGetValue(pid, out var ret);
                var soldQty = sold?.Qty ?? 0m;
                var retQty = ret?.Qty ?? 0m;
                return new ProjectProfitAndLossMaterialItemDto
                {
                    ProductId = pid,
                    ProductTitle = sold?.Title ?? ret?.Title ?? $"کالای #{pid}",
                    ProductUnit = sold?.Unit ?? ret?.Unit,
                    SoldQuantity = soldQty,
                    ReturnedQuantity = retQty,
                    NetQuantity = soldQty - retQty,
                    NetSalesAmount = (sold?.LineTotal ?? 0m) - (ret?.LineTotal ?? 0m),
                    NetCogsAmount = (sold?.Cogs ?? 0m) - (ret?.Cogs ?? 0m)
                };
            })
            .OrderByDescending(m => m.NetCogsAmount)
            .ToList();

            var milestones = await LoadMilestonesInternalAsync(tenantId, projectId, ct);

            return new ProjectProfitAndLossDto
            {
                ProjectId = project.Id,
                ProjectTitle = project.ProjectTitle,
                CustomerName = project.Customer?.Name,
                ProjectLocation = project.ProjectLocation,
                Status = project.Status,
                ProgressPercent = project.ProgressPercent,
                GrossSalesRevenue = grossSalesRevenue,
                SalesReturnsAmount = salesReturnsAmount,
                NetSalesRevenue = netSalesRevenue,
                ProformaSalesTotal = proformaSalesTotal,
                FinalInvoicesCount = finalSales.Count,
                ReturnInvoicesCount = returnInvoices.Count,
                ProformaInvoicesCount = proformaSales.Count,
                GrossMaterialCogs = grossMaterialCogs,
                ReturnedMaterialCogs = returnedMaterialCogs,
                NetMaterialCogs = netMaterialCogs,
                TotalDirectExpenses = totalDirectExpenses,
                TotalApprovedLaborCost = totalApprovedLaborCost,
                TotalApprovedHours = totalApprovedHours,
                PendingLaborCostEstimate = pendingLaborCostEstimate,
                PendingHours = pendingHours,
                TotalCosts = totalCosts,
                GrossProfit = grossProfit,
                NetProfit = netProfit,
                ProfitMarginPercent = marginPercent,
                EstimatedBudget = project.EstimatedBudget,
                BudgetVariance = budgetVariance,
                BudgetUsedPercent = budgetUsedPercent,
                IsOverBudget = isOverBudget,
                Invoices = salesInvoices
                    .Where(s => s.Status == InvoiceStatus.Final || s.Status == InvoiceStatus.Proforma)
                    .Select(s => new ProjectProfitAndLossInvoiceItemDto
                    {
                        Id = s.Id,
                        Number = s.Number,
                        IssueDate = s.IssueDate,
                        IssueDateShamsi = ConvertDate.ConvertMiladiToShamsi(s.IssueDate, "yyyy/MM/dd"),
                        Status = s.Status,
                        StatusTitle = s.Status == InvoiceStatus.Final ? "نهایی" : "پیش‌فاکتور",
                        Subtotal = s.Subtotal,
                        Tax = s.Tax,
                        Discount = s.Discount,
                        Total = s.Total,
                        MaterialCost = s.Items.Where(i => !i.IsDeleted).Sum(i => i.TotalCost),
                        Description = s.Description
                    }).ToList(),
                ReturnInvoices = returnInvoices.Select(r => new ProjectProfitAndLossInvoiceItemDto
                {
                    Id = r.Id,
                    Number = r.Number,
                    IssueDate = r.IssueDate,
                    IssueDateShamsi = ConvertDate.ConvertMiladiToShamsi(r.IssueDate, "yyyy/MM/dd"),
                    Status = r.Status,
                    StatusTitle = "برگشت از فروش (نهایی)",
                    Subtotal = r.Subtotal,
                    Tax = r.Tax,
                    Discount = r.Discount,
                    Total = r.Total,
                    MaterialCost = r.Items.Where(i => !i.IsDeleted).Sum(i => i.TotalCost),
                    Description = r.Description
                }).ToList(),
                DirectExpenses = expenses.Select(e => new ProjectProfitAndLossExpenseItemDto
                {
                    Id = e.Id,
                    ExpenseTitle = e.ExpenseTitle,
                    TypeTitle = e.Type switch
                    {
                        ExpenseType.Transport => "حمل و نقل",
                        ExpenseType.Food => "تغذیه و تدارکات",
                        ExpenseType.Accommodation => "اسکان و ایاب‌وذهاب",
                        _ => "سایر هزینه‌ها"
                    },
                    ExpenseDate = e.ExpenseDate,
                    ExpenseDateShamsi = ConvertDate.ConvertMiladiToShamsi(e.ExpenseDate, "yyyy/MM/dd"),
                    Amount = e.Amount,
                    Description = e.Description
                }).ToList(),
                LaborByMembers = laborByMembers,
                MaterialItems = materialItems,
                Milestones = milestones
            };
        }

        private async Task EnsureProjectAccessAsync(int tenantId, string actorUserId, int projectId, CancellationToken ct)
        {
            if (!await _projectAccess.CanAccessProjectAsync(tenantId, actorUserId, projectId, ct))
                throw new UnauthorizedAccessException("شما مجوز دسترسی به این پروژه را ندارید.");
        }

        private async Task<List<ProjectMilestoneDto>> LoadMilestonesInternalAsync(int tenantId, int projectId, CancellationToken ct)
        {
            var milestones = await _context.ProjectMilestones
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Include(m => m.Project)
                .Where(m => m.TenantId == tenantId && m.ProjectId == projectId && !m.IsDeleted)
                .OrderBy(m => m.SortOrder)
                .ThenBy(m => m.Id)
                .ToListAsync(ct);

            var logCounts = await _context.ProjectProgressLogs
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(l => l.TenantId == tenantId && l.ProjectId == projectId && !l.IsDeleted && l.MilestoneId.HasValue)
                .GroupBy(l => l.MilestoneId!.Value)
                .Select(g => new { MilestoneId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.MilestoneId, x => x.Count, ct);

            return milestones.Select(m =>
            {
                logCounts.TryGetValue(m.Id, out var cnt);
                return MapMilestoneDto(m, m.Project?.ProjectTitle ?? string.Empty, cnt);
            }).ToList();
        }

        private async Task RecalculateProjectProgressAsync(int tenantId, int projectId, CancellationToken ct)
        {
            var project = await _context.Projects
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == tenantId && !p.IsDeleted, ct);
            if (project == null)
                return;

            var milestones = await _context.ProjectMilestones
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(m => m.TenantId == tenantId && m.ProjectId == projectId && !m.IsDeleted)
                .ToListAsync(ct);

            decimal newProgress;
            if (milestones.Count > 0)
            {
                var totalWeight = milestones.Sum(m => m.WeightPercent);
                if (totalWeight > 0m)
                {
                    newProgress = milestones.Sum(m => m.ProgressPercent * m.WeightPercent) / totalWeight;
                }
                else
                {
                    newProgress = milestones.Average(m => m.ProgressPercent);
                }
            }
            else
            {
                var latestLogPct = await _context.ProjectProgressLogs
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(l => l.TenantId == tenantId && l.ProjectId == projectId && !l.IsDeleted)
                    .OrderByDescending(l => l.LogDate)
                    .ThenByDescending(l => l.Id)
                    .Select(l => (decimal?)l.ProgressPercent)
                    .FirstOrDefaultAsync(ct);

                newProgress = latestLogPct ?? 0m;
            }

            project.ProgressPercent = Math.Clamp(Math.Round(newProgress, 2, MidpointRounding.AwayFromZero), 0m, 100m);
            await _context.SaveChangesAsync(ct);
        }

        private static MilestoneStatus DeriveMilestoneStatus(decimal progressPercent, MilestoneStatus? explicitStatus)
        {
            if (progressPercent >= 100m)
                return MilestoneStatus.Completed;
            if (explicitStatus == MilestoneStatus.Delayed)
                return MilestoneStatus.Delayed;
            if (progressPercent > 0m)
                return MilestoneStatus.InProgress;
            return explicitStatus ?? MilestoneStatus.NotStarted;
        }

        private static void ValidateUploadFile(ProjectAttachmentUpload file)
        {
            if (file.Length <= 0 || file.Content.Length == 0)
                return;
            if (file.Length > MaxAttachmentSizeBytes)
                throw new ArgumentException($"حجم فایل «{file.FileName}» نباید بیشتر از ۱۰ مگابایت باشد.");

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
                throw new ArgumentException($"فرمت فایل «{file.FileName}» مجاز نیست. فرمت‌های مجاز: تصویر (jpg/png/webp/gif) و اسناد (pdf/docx/xlsx/txt).");
        }

        private static DateTime ParseDate(string rawDate)
        {
            var clean = rawDate.Trim().Fa2En();
            if (DateTime.TryParseExact(clean, new[] { "yyyy-MM-dd", "yyyy/MM/dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                && parsed.Year >= 1700)
            {
                return parsed.Date;
            }

            return ConvertDate.ConvertShamsiToMiladi(clean).Date;
        }

        private static ProjectMilestoneDto MapMilestoneDto(ProjectMilestone m, string projectTitle, int logsCount) => new()
        {
            Id = m.Id,
            ProjectId = m.ProjectId,
            ProjectTitle = projectTitle,
            Title = m.Title,
            Description = m.Description,
            SortOrder = m.SortOrder,
            WeightPercent = m.WeightPercent,
            ProgressPercent = m.ProgressPercent,
            DueDate = m.DueDate,
            DueDateShamsi = m.DueDate.HasValue ? ConvertDate.ConvertMiladiToShamsi(m.DueDate.Value, "yyyy/MM/dd") : null,
            CompletedAt = m.CompletedAt,
            CompletedAtShamsi = m.CompletedAt.HasValue ? ConvertDate.ConvertMiladiToShamsi(m.CompletedAt.Value, "yyyy/MM/dd") : null,
            Status = m.Status,
            StatusTitle = m.Status.DisplayNameAttribute(),
            LogsCount = logsCount
        };

        private static ProjectAttachmentDto MapAttachmentDto(Attachment a, ProjectProgressLog? parentLog)
        {
            var ext = Path.GetExtension(a.FileName);
            var isImage = (!string.IsNullOrWhiteSpace(a.ContentType) && a.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                          || (!string.IsNullOrWhiteSpace(ext) && ImageExtensions.Contains(ext));

            return new ProjectAttachmentDto
            {
                Id = a.Id,
                FileName = a.FileName,
                ContentType = a.ContentType,
                StoragePath = a.StoragePath,
                IsImage = isImage,
                ProgressLogId = a.EntityId,
                ProgressLogTitle = parentLog?.Title ?? string.Empty,
                LogDateShamsi = parentLog != null ? ConvertDate.ConvertMiladiToShamsi(parentLog.LogDate, "yyyy/MM/dd") : string.Empty,
                UploadedByName = parentLog?.RecordedByUser != null
                    ? $"{parentLog.RecordedByUser.UserFirstName} {parentLog.RecordedByUser.UserLastName}".Trim()
                    : string.Empty,
                CreatedAt = a.CreatedAt
            };
        }

        private static ProjectProgressLogDto MapProgressLogDto(ProjectProgressLog l, List<Attachment> attachments) => new()
        {
            Id = l.Id,
            ProjectId = l.ProjectId,
            ProjectTitle = l.Project?.ProjectTitle ?? string.Empty,
            MilestoneId = l.MilestoneId,
            MilestoneTitle = l.Milestone?.Title,
            FiscalYearId = l.FiscalYearId,
            RecordedByUserId = l.RecordedByUserId,
            RecordedByName = l.RecordedByUser != null
                ? $"{l.RecordedByUser.UserFirstName} {l.RecordedByUser.UserLastName}".Trim()
                : string.Empty,
            LogDate = l.LogDate,
            LogDateShamsi = ConvertDate.ConvertMiladiToShamsi(l.LogDate, "yyyy/MM/dd"),
            Title = l.Title,
            ReportText = l.ReportText,
            ProgressPercent = l.ProgressPercent,
            WorkshopArea = l.WorkshopArea,
            CreatedAt = l.CreatedAt,
            Attachments = attachments.Select(a => MapAttachmentDto(a, l)).ToList()
        };
    }
}
