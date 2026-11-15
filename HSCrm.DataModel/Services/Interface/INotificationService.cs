using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;

namespace HSCrm.DataModel.Services.Interface
{
    public interface INotificationService
    {
        Task<NotificationFeedDto> GetMyNotificationsAsync(int tenantId, string userId, bool includeChequeAlerts, int limit = 30, CancellationToken ct = default);
        Task<bool> MarkAsReadAsync(int tenantId, string userId, int notificationId, CancellationToken ct = default);
        Task<int> MarkAllAsReadAsync(int tenantId, string userId, CancellationToken ct = default);
        Task NotifyWorkLogSubmittedAsync(int tenantId, ProjectWorkLog workLog, string projectTitle, string employeeName, string actorUserId, CancellationToken ct = default);
        Task NotifyWorkLogReviewedAsync(int tenantId, ProjectWorkLog workLog, string projectTitle, string reviewerName, bool isApproved, string? reviewNote, CancellationToken ct = default);
        Task NotifyProjectStatusChangedAsync(int tenantId, Project project, string oldStatus, string newStatus, string actorUserId, string? note = null, CancellationToken ct = default);
    }
}
