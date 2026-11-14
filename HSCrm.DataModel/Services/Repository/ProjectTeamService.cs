using System.Globalization;
using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using Microsoft.EntityFrameworkCore;
using HSCrm.Common.PublicTools;
using SinaPharm.Common;
using static HSCrm.Common.PublicTools.Enums;
using static HSCrm.Common.PublicTools.StringCleaner;

namespace HSCrm.DataModel.Services.Repository
{
    public class ProjectTeamService : IProjectTeamService
    {
        private readonly ApplicationDbContext _context;
        private readonly IProjectAccessService _projectAccess;

        public ProjectTeamService(ApplicationDbContext context, IProjectAccessService projectAccess)
        {
            _context = context;
            _projectAccess = projectAccess;
        }

        public async Task<List<ProjectMemberDto>> GetProjectMembersAsync(int tenantId, int projectId, CancellationToken ct = default)
        {
            var members = await _context.ProjectMembers
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Include(m => m.Project)
                .Include(m => m.User)
                .Where(m => m.TenantId == tenantId && m.ProjectId == projectId && !m.IsDeleted)
                .OrderByDescending(m => m.IsActive)
                .ThenBy(m => m.User.UserFirstName)
                .ThenBy(m => m.User.UserLastName)
                .ToListAsync(ct);

            return members.Select(MapMemberDto).ToList();
        }

        public async Task<Dictionary<int, int>> GetProjectMemberCountsAsync(int tenantId, string actorUserId, CancellationToken ct = default)
        {
            var accessibleIds = await _projectAccess.GetAccessibleProjectIdsAsync(tenantId, actorUserId, ct);
            if (accessibleIds.Count == 0)
                return new Dictionary<int, int>();

            return await _context.ProjectMembers
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(m => m.TenantId == tenantId && !m.IsDeleted && m.IsActive && accessibleIds.Contains(m.ProjectId))
                .GroupBy(m => m.ProjectId)
                .Select(g => new { ProjectId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ProjectId, x => x.Count, ct);
        }

        public async Task<List<AssignableUserDto>> GetAssignableUsersAsync(int tenantId, CancellationToken ct = default)
        {
            return await _context.Users
                .AsNoTracking()
                .Where(u => u.TenantId == tenantId && u.UserStatus && u.UserType != 3 && u.UserType != 110)
                .OrderBy(u => u.UserFirstName)
                .ThenBy(u => u.UserLastName)
                .Select(u => new AssignableUserDto
                {
                    Id = u.Id,
                    FullName = (u.UserFirstName + " " + u.UserLastName).Trim(),
                    UserName = u.UserName ?? string.Empty,
                    UserType = u.UserType,
                    UserImageUrl = u.UserImageUrl
                })
                .ToListAsync(ct);
        }

        public async Task<ProjectMemberDto> AddOrUpdateMemberAsync(int tenantId, string actorUserId, ProjectMemberUpsertModel model, CancellationToken ct = default)
        {
            var project = await _context.Projects
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == model.ProjectId && p.TenantId == tenantId && !p.IsDeleted, ct)
                ?? throw new KeyNotFoundException("پروژه مورد نظر یافت نشد.");

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == model.UserId && u.TenantId == tenantId && u.UserType != 3, ct)
                ?? throw new KeyNotFoundException("کاربر مورد نظر در این شرکت یافت نشد.");

            var existing = await _context.ProjectMembers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.ProjectId == model.ProjectId && m.UserId == model.UserId, ct);

            if (existing != null)
            {
                existing.IsDeleted = false;
                existing.DeletedAt = null;
                existing.DeletedById = null;
                existing.IsActive = model.IsActive;
                existing.ProjectRole = string.IsNullOrWhiteSpace(model.ProjectRole) ? null : model.ProjectRole.Trim();
                existing.HourlyRate = model.HourlyRate;
                existing.ModifiedAt = DateTime.UtcNow;
                existing.ModifiedById = actorUserId;
                if (existing.JoinedAt == default)
                    existing.JoinedAt = DateTime.UtcNow;
            }
            else
            {
                existing = new ProjectMember
                {
                    TenantId = tenantId,
                    ProjectId = model.ProjectId,
                    UserId = model.UserId,
                    ProjectRole = string.IsNullOrWhiteSpace(model.ProjectRole) ? null : model.ProjectRole.Trim(),
                    HourlyRate = model.HourlyRate,
                    IsActive = model.IsActive,
                    JoinedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = actorUserId,
                    IsDeleted = false
                };
                _context.ProjectMembers.Add(existing);
            }

            await _context.SaveChangesAsync(ct);

            existing.Project = project;
            existing.User = user;
            return MapMemberDto(existing);
        }

        public async Task<bool> RemoveMemberAsync(int tenantId, int memberId, string actorUserId, CancellationToken ct = default)
        {
            var member = await _context.ProjectMembers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.Id == memberId && m.TenantId == tenantId && !m.IsDeleted, ct);

            if (member == null)
                return false;

            member.IsDeleted = true;
            member.IsActive = false;
            member.DeletedAt = DateTime.UtcNow;
            member.DeletedById = actorUserId;
            member.ModifiedAt = DateTime.UtcNow;
            member.ModifiedById = actorUserId;

            await _context.SaveChangesAsync(ct);
            return true;
        }

        public async Task<List<ProjectWorkLogDto>> GetWorkLogsAsync(
            int tenantId,
            int fiscalYearId,
            string actorUserId,
            int? projectId = null,
            string? targetUserId = null,
            WorkLogStatus? status = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default)
        {
            var fullAccess = await _projectAccess.HasFullProjectAccessAsync(actorUserId, ct);
            var accessibleIds = await _projectAccess.GetAccessibleProjectIdsAsync(tenantId, actorUserId, ct);

            var query = _context.ProjectWorkLogs
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Include(w => w.Project)
                .Include(w => w.User)
                .Include(w => w.ReviewedByUser)
                .Where(w => w.TenantId == tenantId && w.FiscalYearId == fiscalYearId && !w.IsDeleted);

            if (!fullAccess)
            {
                query = query.Where(w => w.UserId == actorUserId && accessibleIds.Contains(w.ProjectId));
            }

            if (projectId.HasValue && projectId.Value > 0)
            {
                if (!fullAccess && !accessibleIds.Contains(projectId.Value))
                    throw new UnauthorizedAccessException("شما عضو این پروژه نیستید و مجاز به مشاهده کارکرد آن نمی‌باشید.");
                query = query.Where(w => w.ProjectId == projectId.Value);
            }

            if (fullAccess && !string.IsNullOrWhiteSpace(targetUserId))
                query = query.Where(w => w.UserId == targetUserId);

            if (status.HasValue)
                query = query.Where(w => w.Status == status.Value);

            if (fromDate.HasValue)
                query = query.Where(w => w.WorkDate >= fromDate.Value.Date);

            if (toDate.HasValue)
                query = query.Where(w => w.WorkDate < toDate.Value.Date.AddDays(1));

            var logs = await query
                .OrderByDescending(w => w.WorkDate)
                .ThenByDescending(w => w.Id)
                .ToListAsync(ct);

            return logs.Select(MapWorkLogDto).ToList();
        }

        public async Task<ProjectWorkLogDto> CreateWorkLogAsync(int tenantId, int fiscalYearId, string actorUserId, ProjectWorkLogCreateModel model, CancellationToken ct = default)
        {
            if (model.Hours <= 0 || model.Hours > 24)
                throw new ArgumentException("ساعت کار باید بین ۰٫۲۵ تا ۲۴ ساعت باشد.");

            if (string.IsNullOrWhiteSpace(model.Description))
                throw new ArgumentException("شرح فعالیت انجام‌شده الزامی است.");

            var canAccess = await _projectAccess.CanAccessProjectAsync(tenantId, actorUserId, model.ProjectId, ct);
            if (!canAccess)
                throw new UnauthorizedAccessException("شما عضو این پروژه نیستید و مجاز به ثبت ساعت کار روی آن نمی‌باشید.");

            var project = await _context.Projects
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == model.ProjectId && p.TenantId == tenantId && !p.IsDeleted, ct)
                ?? throw new KeyNotFoundException("پروژه مورد نظر یافت نشد.");

            var workDate = ParseWorkDate(model.WorkDate);
            await EnsureDateNotLockedAsync(tenantId, workDate, ct);

            var membership = await _context.ProjectMembers
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.ProjectId == model.ProjectId && m.UserId == actorUserId && m.IsActive && !m.IsDeleted, ct);

            var rate = membership?.HourlyRate ?? 0m;
            var wageCost = Math.Round(model.Hours * rate, 2, MidpointRounding.AwayFromZero);

            var log = new ProjectWorkLog
            {
                TenantId = tenantId,
                FiscalYearId = fiscalYearId,
                ProjectId = model.ProjectId,
                UserId = actorUserId,
                WorkDate = workDate,
                Hours = model.Hours,
                HourlyRateSnapshot = rate,
                WageCost = wageCost,
                Description = model.Description.Trim(),
                Status = WorkLogStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                CreatedById = actorUserId
            };

            _context.ProjectWorkLogs.Add(log);
            await _context.SaveChangesAsync(ct);

            log.Project = project;
            log.User = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == actorUserId, ct);
            return MapWorkLogDto(log);
        }

        public async Task<ProjectWorkLogDto> UpdateWorkLogAsync(int tenantId, int fiscalYearId, string actorUserId, ProjectWorkLogEditModel model, CancellationToken ct = default)
        {
            if (model.Hours <= 0 || model.Hours > 24)
                throw new ArgumentException("ساعت کار باید بین ۰٫۲۵ تا ۲۴ ساعت باشد.");

            var log = await _context.ProjectWorkLogs
                .IgnoreQueryFilters()
                .Include(w => w.Project)
                .Include(w => w.User)
                .FirstOrDefaultAsync(w => w.Id == model.Id && w.TenantId == tenantId && w.FiscalYearId == fiscalYearId && !w.IsDeleted, ct)
                ?? throw new KeyNotFoundException("رکورد ساعت کار یافت نشد.");

            var fullAccess = await _projectAccess.HasFullProjectAccessAsync(actorUserId, ct);
            if (!fullAccess && log.UserId != actorUserId)
                throw new UnauthorizedAccessException("فقط مجاز به ویرایش ساعات کاری ثبت‌شده خودتان هستید.");

            if (log.Status != WorkLogStatus.Pending)
                throw new InvalidOperationException("فقط ساعات کاری در وضعیت «در انتظار تأیید» قابل ویرایش هستند.");

            if (model.ProjectId != log.ProjectId)
            {
                var canAccessTarget = await _projectAccess.CanAccessProjectAsync(tenantId, log.UserId, model.ProjectId, ct);
                if (!canAccessTarget && !fullAccess)
                    throw new UnauthorizedAccessException("کاربر عضو پروژه انتخاب‌شده نیست.");
                var targetProject = await _context.Projects
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(p => p.Id == model.ProjectId && p.TenantId == tenantId && !p.IsDeleted, ct)
                    ?? throw new KeyNotFoundException("پروژه انتخاب‌شده یافت نشد.");
                log.ProjectId = model.ProjectId;
                log.Project = targetProject;
            }

            var workDate = ParseWorkDate(model.WorkDate);
            await EnsureDateNotLockedAsync(tenantId, log.WorkDate, ct);
            await EnsureDateNotLockedAsync(tenantId, workDate, ct);

            var membership = await _context.ProjectMembers
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.ProjectId == log.ProjectId && m.UserId == log.UserId && m.IsActive && !m.IsDeleted, ct);

            var rate = membership?.HourlyRate ?? log.HourlyRateSnapshot;

            log.WorkDate = workDate;
            log.Hours = model.Hours;
            log.HourlyRateSnapshot = rate;
            log.WageCost = Math.Round(model.Hours * rate, 2, MidpointRounding.AwayFromZero);
            log.Description = model.Description.Trim();
            log.ModifiedAt = DateTime.UtcNow;
            log.ModifiedById = actorUserId;

            await _context.SaveChangesAsync(ct);
            return MapWorkLogDto(log);
        }

        public async Task<bool> DeleteWorkLogAsync(int tenantId, int fiscalYearId, string actorUserId, int id, CancellationToken ct = default)
        {
            var log = await _context.ProjectWorkLogs
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(w => w.Id == id && w.TenantId == tenantId && w.FiscalYearId == fiscalYearId && !w.IsDeleted, ct);

            if (log == null)
                return false;

            var fullAccess = await _projectAccess.HasFullProjectAccessAsync(actorUserId, ct);
            if (!fullAccess && log.UserId != actorUserId)
                throw new UnauthorizedAccessException("فقط مجاز به حذف ساعات کاری ثبت‌شده خودتان هستید.");

            if (log.Status != WorkLogStatus.Pending)
                throw new InvalidOperationException("فقط ساعات کاری در وضعیت «در انتظار تأیید» قابل حذف هستند.");

            await EnsureDateNotLockedAsync(tenantId, log.WorkDate, ct);

            log.IsDeleted = true;
            log.DeletedAt = DateTime.UtcNow;
            log.DeletedById = actorUserId;
            log.ModifiedAt = DateTime.UtcNow;
            log.ModifiedById = actorUserId;

            await _context.SaveChangesAsync(ct);
            return true;
        }

        public async Task<ProjectWorkLogDto> ReviewWorkLogAsync(int tenantId, int fiscalYearId, string reviewerUserId, ProjectWorkLogReviewModel model, CancellationToken ct = default)
        {
            var log = await _context.ProjectWorkLogs
                .IgnoreQueryFilters()
                .Include(w => w.Project)
                .Include(w => w.User)
                .FirstOrDefaultAsync(w => w.Id == model.WorkLogId && w.TenantId == tenantId && w.FiscalYearId == fiscalYearId && !w.IsDeleted, ct)
                ?? throw new KeyNotFoundException("رکورد ساعت کار یافت نشد.");

            await EnsureDateNotLockedAsync(tenantId, log.WorkDate, ct);

            if (!model.IsApproved && string.IsNullOrWhiteSpace(model.ReviewNote))
                throw new ArgumentException("در صورت رد ساعت کار، ذکر علت در یادداشت الزامی است.");

            if (model.OverrideHourlyRate.HasValue)
            {
                log.HourlyRateSnapshot = model.OverrideHourlyRate.Value;
                log.WageCost = Math.Round(log.Hours * model.OverrideHourlyRate.Value, 2, MidpointRounding.AwayFromZero);
            }
            else if (log.HourlyRateSnapshot == 0m)
            {
                var membership = await _context.ProjectMembers
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.ProjectId == log.ProjectId && m.UserId == log.UserId && !m.IsDeleted, ct);
                if (membership?.HourlyRate is > 0m)
                {
                    log.HourlyRateSnapshot = membership.HourlyRate.Value;
                    log.WageCost = Math.Round(log.Hours * log.HourlyRateSnapshot, 2, MidpointRounding.AwayFromZero);
                }
            }

            log.Status = model.IsApproved ? WorkLogStatus.Approved : WorkLogStatus.Rejected;
            log.ReviewedByUserId = reviewerUserId;
            log.ReviewedAt = DateTime.UtcNow;
            log.ReviewNote = string.IsNullOrWhiteSpace(model.ReviewNote) ? null : model.ReviewNote.Trim();
            log.ModifiedAt = DateTime.UtcNow;
            log.ModifiedById = reviewerUserId;

            await _context.SaveChangesAsync(ct);
            log.ReviewedByUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == reviewerUserId, ct);
            return MapWorkLogDto(log);
        }

        public async Task<int> BatchReviewWorkLogsAsync(int tenantId, int fiscalYearId, string reviewerUserId, ProjectWorkLogBatchReviewModel model, CancellationToken ct = default)
        {
            if (model.WorkLogIds == null || model.WorkLogIds.Count == 0)
                return 0;

            if (!model.IsApproved && string.IsNullOrWhiteSpace(model.ReviewNote))
                throw new ArgumentException("در صورت رد گروهی ساعات کار، ذکر علت در یادداشت الزامی است.");

            var logs = await _context.ProjectWorkLogs
                .IgnoreQueryFilters()
                .Where(w => w.TenantId == tenantId && w.FiscalYearId == fiscalYearId && !w.IsDeleted && model.WorkLogIds.Contains(w.Id))
                .ToListAsync(ct);

            foreach (var log in logs)
            {
                await EnsureDateNotLockedAsync(tenantId, log.WorkDate, ct);
                log.Status = model.IsApproved ? WorkLogStatus.Approved : WorkLogStatus.Rejected;
                log.ReviewedByUserId = reviewerUserId;
                log.ReviewedAt = DateTime.UtcNow;
                log.ReviewNote = string.IsNullOrWhiteSpace(model.ReviewNote) ? null : model.ReviewNote.Trim();
                log.ModifiedAt = DateTime.UtcNow;
                log.ModifiedById = reviewerUserId;
            }

            await _context.SaveChangesAsync(ct);
            return logs.Count;
        }

        public async Task<EmployeeDashboardDto> GetEmployeeDashboardAsync(int tenantId, int fiscalYearId, string userId, CancellationToken ct = default)
        {
            var isFullAccess = await _projectAccess.HasFullProjectAccessAsync(userId, ct);
            var memberships = await _projectAccess.GetUserMembershipsAsync(tenantId, userId, ct);
            var membershipByProj = memberships.ToDictionary(m => m.ProjectId, m => m);

            var accessibleIds = await _projectAccess.GetAccessibleProjectIdsAsync(tenantId, userId, ct);

            var projects = accessibleIds.Count > 0
                ? await _context.Projects
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Include(p => p.Customer)
                    .Where(p => p.TenantId == tenantId && !p.IsDeleted && accessibleIds.Contains(p.Id))
                    .OrderByDescending(p => p.Id)
                    .ToListAsync(ct)
                : new List<Project>();

            var myLogs = await _context.ProjectWorkLogs
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Include(w => w.Project)
                .Include(w => w.User)
                .Include(w => w.ReviewedByUser)
                .Where(w => w.TenantId == tenantId && w.FiscalYearId == fiscalYearId && !w.IsDeleted && w.UserId == userId)
                .OrderByDescending(w => w.WorkDate)
                .ThenByDescending(w => w.Id)
                .ToListAsync(ct);

            var projectDtos = projects.Select(p =>
            {
                membershipByProj.TryGetValue(p.Id, out var mem);
                var pLogs = myLogs.Where(l => l.ProjectId == p.Id).ToList();
                return new EmployeeProjectSummaryDto
                {
                    ProjectId = p.Id,
                    ProjectTitle = p.ProjectTitle,
                    CustomerName = p.Customer?.Name,
                    ProjectLocation = p.ProjectLocation,
                    ProjectRole = mem?.ProjectRole ?? (isFullAccess ? "دسترسی مدیریتی" : "عضو تیم"),
                    HourlyRate = mem?.HourlyRate,
                    MyTotalHours = pLogs.Sum(l => l.Hours),
                    MyApprovedHours = pLogs.Where(l => l.Status == WorkLogStatus.Approved).Sum(l => l.Hours),
                    MyPendingHours = pLogs.Where(l => l.Status == WorkLogStatus.Pending).Sum(l => l.Hours),
                    MyApprovedWageCost = pLogs.Where(l => l.Status == WorkLogStatus.Approved).Sum(l => l.WageCost)
                };
            }).ToList();

            return new EmployeeDashboardDto
            {
                IsFullAccess = isFullAccess,
                AssignedProjectsCount = projectDtos.Count,
                TotalLoggedHours = myLogs.Sum(l => l.Hours),
                ApprovedHours = myLogs.Where(l => l.Status == WorkLogStatus.Approved).Sum(l => l.Hours),
                PendingHours = myLogs.Where(l => l.Status == WorkLogStatus.Pending).Sum(l => l.Hours),
                RejectedHours = myLogs.Where(l => l.Status == WorkLogStatus.Rejected).Sum(l => l.Hours),
                ApprovedWageCost = myLogs.Where(l => l.Status == WorkLogStatus.Approved).Sum(l => l.WageCost),
                Projects = projectDtos,
                RecentWorkLogs = myLogs.Select(MapWorkLogDto).ToList()
            };
        }

        private async Task EnsureDateNotLockedAsync(int tenantId, DateTime date, CancellationToken ct)
        {
            var lockDate = await _context.Tenants
                .AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => t.DocumentLockDate)
                .FirstOrDefaultAsync(ct);

            if (lockDate.HasValue && date.Date <= lockDate.Value.Date)
                throw new InvalidOperationException("ثبت یا تغییر ساعت کار در تاریخ‌های قفل‌شده امکان‌پذیر نیست.");
        }

        private static DateTime ParseWorkDate(string rawDate)
        {
            var clean = rawDate.Trim().Fa2En();
            if (DateTime.TryParseExact(clean, new[] { "yyyy-MM-dd", "yyyy/MM/dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                && parsed.Year >= 1700)
            {
                return parsed.Date;
            }

            return ConvertDate.ConvertShamsiToMiladi(clean).Date;
        }

        private static ProjectMemberDto MapMemberDto(ProjectMember m) => new()
        {
            Id = m.Id,
            ProjectId = m.ProjectId,
            ProjectTitle = m.Project?.ProjectTitle ?? string.Empty,
            UserId = m.UserId,
            FullName = m.User != null ? $"{m.User.UserFirstName} {m.User.UserLastName}".Trim() : string.Empty,
            UserName = m.User?.UserName ?? string.Empty,
            UserImageUrl = m.User?.UserImageUrl,
            ProjectRole = m.ProjectRole,
            HourlyRate = m.HourlyRate,
            IsActive = m.IsActive,
            JoinedAt = m.JoinedAt,
            JoinedAtShamsi = ConvertDate.ConvertMiladiToShamsi(m.JoinedAt, "yyyy/MM/dd")
        };

        private static ProjectWorkLogDto MapWorkLogDto(ProjectWorkLog w) => new()
        {
            Id = w.Id,
            ProjectId = w.ProjectId,
            ProjectTitle = w.Project?.ProjectTitle ?? string.Empty,
            UserId = w.UserId,
            EmployeeName = w.User != null ? $"{w.User.UserFirstName} {w.User.UserLastName}".Trim() : string.Empty,
            FiscalYearId = w.FiscalYearId,
            WorkDate = w.WorkDate,
            WorkDateShamsi = ConvertDate.ConvertMiladiToShamsi(w.WorkDate, "yyyy/MM/dd"),
            Hours = w.Hours,
            HourlyRateSnapshot = w.HourlyRateSnapshot,
            WageCost = w.WageCost,
            Description = w.Description,
            Status = w.Status,
            StatusTitle = w.Status.DisplayNameAttribute(),
            ReviewedByUserId = w.ReviewedByUserId,
            ReviewedByName = w.ReviewedByUser != null ? $"{w.ReviewedByUser.UserFirstName} {w.ReviewedByUser.UserLastName}".Trim() : null,
            ReviewedAt = w.ReviewedAt,
            ReviewedAtShamsi = w.ReviewedAt.HasValue ? ConvertDate.ConvertMiladiToShamsi(w.ReviewedAt.Value, "yyyy/MM/dd HH:mm") : null,
            ReviewNote = w.ReviewNote,
            CreatedAt = w.CreatedAt
        };
    }
}
