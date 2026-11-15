using Common.Permissions;
using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using Microsoft.EntityFrameworkCore;
using SinaPharm.Common;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.DataModel.Services.Repository
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<NotificationFeedDto> GetMyNotificationsAsync(
            int tenantId,
            string userId,
            bool includeChequeAlerts,
            int limit = 30,
            CancellationToken ct = default)
        {
            var clampedLimit = Math.Clamp(limit, 5, 100);

            var persistent = await _context.AppNotifications
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(n => n.TenantId == tenantId && n.RecipientUserId == userId && !n.IsDeleted)
                .OrderBy(n => n.IsRead)
                .ThenByDescending(n => n.CreatedAt)
                .Take(clampedLimit)
                .ToListAsync(ct);

            var items = persistent.Select(n => new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                Category = n.Category,
                Severity = n.Severity,
                ReferenceEntity = n.ReferenceEntity,
                ReferenceId = n.ReferenceId,
                ActionUrl = n.ActionUrl,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
                CreatedAtShamsi = ConvertDate.ConvertMiladiToShamsi(n.CreatedAt, "yyyy/MM/dd HH:mm"),
                IsDynamicAlert = false
            }).ToList();

            var unreadCount = await _context.AppNotifications
                .AsNoTracking()
                .IgnoreQueryFilters()
                .CountAsync(n => n.TenantId == tenantId && n.RecipientUserId == userId && !n.IsDeleted && !n.IsRead, ct);

            if (includeChequeAlerts)
            {
                var threshold = DateTime.UtcNow.AddDays(7);
                var dueCheques = await _context.Cheques
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(c => c.TenantId == tenantId
                             && !c.IsDeleted
                             && (c.Status == ChequeStatus.InSafe || c.Status == ChequeStatus.DepositedToBank)
                             && c.DueDate <= threshold)
                    .OrderBy(c => c.DueDate)
                    .Take(10)
                    .ToListAsync(ct);

                foreach (var chq in dueCheques)
                {
                    var isOverdue = chq.DueDate.Date < DateTime.UtcNow.Date;
                    var typeFa = chq.Type == ChequeType.Receivable ? "چک دریافتنی" : "چک پرداختنی";
                    items.Insert(0, new NotificationDto
                    {
                        Id = null,
                        Title = isOverdue ? $"سررسید گذشته: {typeFa} #{chq.ChequeNumber}" : $"سررسید نزدیک: {typeFa} #{chq.ChequeNumber}",
                        Message = $"مبلغ {chq.Amount:N0} ریال ({chq.BankName}) — سررسید: {ConvertDate.ConvertMiladiToShamsi(chq.DueDate, "yyyy/MM/dd")}",
                        Category = "Cheque",
                        Severity = isOverdue ? "danger" : "warning",
                        ReferenceEntity = "Cheque",
                        ReferenceId = chq.Id,
                        ActionUrl = "/AdminArea/Cheque/Index",
                        IsRead = false,
                        CreatedAt = chq.DueDate,
                        CreatedAtShamsi = ConvertDate.ConvertMiladiToShamsi(chq.DueDate, "yyyy/MM/dd"),
                        IsDynamicAlert = true
                    });
                }

                unreadCount += dueCheques.Count;
            }

            return new NotificationFeedDto
            {
                UnreadCount = unreadCount,
                Items = items.Take(clampedLimit).ToList()
            };
        }

        public async Task<bool> MarkAsReadAsync(int tenantId, string userId, int notificationId, CancellationToken ct = default)
        {
            var notif = await _context.AppNotifications
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.TenantId == tenantId && n.RecipientUserId == userId && !n.IsDeleted, ct);

            if (notif == null)
                return false;

            if (!notif.IsRead)
            {
                notif.IsRead = true;
                notif.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
            }
            return true;
        }

        public async Task<int> MarkAllAsReadAsync(int tenantId, string userId, CancellationToken ct = default)
        {
            var unread = await _context.AppNotifications
                .IgnoreQueryFilters()
                .Where(n => n.TenantId == tenantId && n.RecipientUserId == userId && !n.IsDeleted && !n.IsRead)
                .ToListAsync(ct);

            if (unread.Count == 0)
                return 0;

            var now = DateTime.UtcNow;
            foreach (var n in unread)
            {
                n.IsRead = true;
                n.ReadAt = now;
            }
            await _context.SaveChangesAsync(ct);
            return unread.Count;
        }

        public async Task NotifyWorkLogSubmittedAsync(
            int tenantId,
            ProjectWorkLog workLog,
            string projectTitle,
            string employeeName,
            string actorUserId,
            CancellationToken ct = default)
        {
            var approverIds = await GetTenantManagerAndApproverUserIdsAsync(tenantId, ct);
            approverIds.Remove(actorUserId);
            if (approverIds.Count == 0) return;

            var now = DateTime.UtcNow;
            var shamsi = ConvertDate.ConvertMiladiToShamsi(workLog.WorkDate, "yyyy/MM/dd");
            foreach (var recipientId in approverIds)
            {
                _context.AppNotifications.Add(new AppNotification
                {
                    TenantId = tenantId,
                    RecipientUserId = recipientId,
                    Title = $"ساعت کار جدید در انتظار تأیید ({projectTitle})",
                    Message = $"{employeeName} به میزان {workLog.Hours:0.##} ساعت کار برای تاریخ {shamsi} ثبت کرد.",
                    Category = "WorkLog",
                    Severity = "info",
                    ReferenceEntity = "ProjectWorkLog",
                    ReferenceId = workLog.Id,
                    ActionUrl = "/AdminArea/ProjectWorkLog/Index",
                    IsRead = false,
                    CreatedAt = now,
                    CreatedById = actorUserId
                });
            }
            await _context.SaveChangesAsync(ct);
        }

        public async Task NotifyWorkLogReviewedAsync(
            int tenantId,
            ProjectWorkLog workLog,
            string projectTitle,
            string reviewerName,
            bool isApproved,
            string? reviewNote,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(workLog.UserId))
                return;

            var shamsi = ConvertDate.ConvertMiladiToShamsi(workLog.WorkDate, "yyyy/MM/dd");
            var statusLabel = isApproved ? "تأیید شد" : "رد شد";
            var notePart = string.IsNullOrWhiteSpace(reviewNote) ? string.Empty : $" (یادداشت: {reviewNote.Trim()})";

            _context.AppNotifications.Add(new AppNotification
            {
                TenantId = tenantId,
                RecipientUserId = workLog.UserId,
                Title = $"ساعت کار شما {statusLabel}",
                Message = $"کارکرد {workLog.Hours:0.##} ساعت تاریخ {shamsi} در پروژه «{projectTitle}» توسط {reviewerName} {statusLabel}.{notePart}",
                Category = "WorkLog",
                Severity = isApproved ? "success" : "danger",
                ReferenceEntity = "ProjectWorkLog",
                ReferenceId = workLog.Id,
                ActionUrl = "/AdminArea/Employee/Index",
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                CreatedById = workLog.ReviewedByUserId
            });

            await _context.SaveChangesAsync(ct);
        }

        public async Task NotifyProjectStatusChangedAsync(
            int tenantId,
            Project project,
            string oldStatus,
            string newStatus,
            string actorUserId,
            string? note = null,
            CancellationToken ct = default)
        {
            var oldTitle = TranslateProjectStatus(oldStatus);
            var newTitle = TranslateProjectStatus(newStatus);
            var notePart = string.IsNullOrWhiteSpace(note) ? string.Empty : $" — {note.Trim()}";
            var now = DateTime.UtcNow;

            var memberUserIds = await _context.ProjectMembers
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(m => m.TenantId == tenantId && m.ProjectId == project.Id && m.IsActive && !m.IsDeleted)
                .Select(m => m.UserId)
                .Distinct()
                .ToListAsync(ct);

            foreach (var memberUserId in memberUserIds.Where(uid => !string.Equals(uid, actorUserId, StringComparison.Ordinal)))
            {
                _context.AppNotifications.Add(new AppNotification
                {
                    TenantId = tenantId,
                    RecipientUserId = memberUserId,
                    Title = $"تغییر وضعیت پروژه: {project.ProjectTitle}",
                    Message = $"وضعیت پروژه از «{oldTitle}» به «{newTitle}» تغییر یافت.{notePart}",
                    Category = "Project",
                    Severity = string.Equals(newStatus, "Completed", StringComparison.OrdinalIgnoreCase) ? "success" : "info",
                    ReferenceEntity = "Project",
                    ReferenceId = project.Id,
                    ActionUrl = "/AdminArea/Project/Index",
                    IsRead = false,
                    CreatedAt = now,
                    CreatedById = actorUserId
                });
            }

            if (project.CustomerId.HasValue)
            {
                var customerUserId = await _context.Customers
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(c => c.TenantId == tenantId && c.Id == project.CustomerId.Value && !c.IsDeleted)
                    .Select(c => c.ApplicationUserId)
                    .FirstOrDefaultAsync(ct);

                if (!string.IsNullOrWhiteSpace(customerUserId) && !string.Equals(customerUserId, actorUserId, StringComparison.Ordinal))
                {
                    _context.AppNotifications.Add(new AppNotification
                    {
                        TenantId = tenantId,
                        RecipientUserId = customerUserId,
                        Title = $"بروزرسانی وضعیت پروژه: {project.ProjectTitle}",
                        Message = $"وضعیت پروژه شما به «{newTitle}» تغییر یافت.{notePart}",
                        Category = "Project",
                        Severity = string.Equals(newStatus, "Completed", StringComparison.OrdinalIgnoreCase) ? "success" : "info",
                        ReferenceEntity = "Project",
                        ReferenceId = project.Id,
                        ActionUrl = $"/UserArea/Project/Details/{project.Id}",
                        IsRead = false,
                        CreatedAt = now,
                        CreatedById = actorUserId
                    });
                }
            }

            await _context.SaveChangesAsync(ct);
        }

        private async Task<HashSet<string>> GetTenantManagerAndApproverUserIdsAsync(int tenantId, CancellationToken ct)
        {
            var ownerIds = await _context.Users
                .AsNoTracking()
                .Where(u => u.TenantId == tenantId && u.UserStatus && u.IsTenantOwner)
                .Select(u => u.Id)
                .ToListAsync(ct);

            var permRoleUserIds = await _context.UserRoles
                .AsNoTracking()
                .Where(ur => ur.User.TenantId == tenantId && ur.User.UserStatus &&
                             (ur.Role.Name == "Owner" || ur.Role.Name == "Admin" ||
                              ur.Role.RolePermissions.Any(rp => rp.Permission.Name == Permissions.WorkLogs.Approve)))
                .Select(ur => ur.UserId)
                .ToListAsync(ct);

            return ownerIds.Concat(permRoleUserIds).ToHashSet(StringComparer.Ordinal);
        }

        private static string TranslateProjectStatus(string? status) => status?.ToLowerInvariant() switch
        {
            "active" => "در حال اجرا",
            "completed" => "تکمیل‌شده",
            "onhold" => "متوقف موقت",
            "cancelled" => "لغوشده",
            _ => status ?? "نامشخص"
        };
    }
}
