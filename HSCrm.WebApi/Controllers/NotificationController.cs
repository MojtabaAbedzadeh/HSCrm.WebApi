using Common.Permissions;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Models;
using HSCrm.Framework.Extensions;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCrm.WebApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly IPermissionService? _permissionService;

        public NotificationController(
            INotificationService notificationService,
            IPermissionService? permissionService = null)
        {
            _notificationService = notificationService;
            _permissionService = permissionService;
        }

        [HttpGet("GetMyNotifications")]
        public async Task<ActionResult<ApiResponse<NotificationFeedDto>>> GetMyNotifications([FromQuery] int limit = 30, CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var userId = User.GetUserId();
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new ApiResponse<NotificationFeedDto> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            var includeChequeAlerts = false;
            if (!User.IsInRole("Customer") && _permissionService != null)
            {
                includeChequeAlerts = await _permissionService.HasPermissionAsync(userId, Permissions.Cheques.View);
            }

            var feed = await _notificationService.GetMyNotificationsAsync(tenantId.Value, userId, includeChequeAlerts, limit, ct);
            return Ok(new ApiResponse<NotificationFeedDto>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "اعلان‌های کاربر دریافت شد.",
                Data = feed
            });
        }

        [HttpPost("MarkAsRead/{id:int}")]
        public async Task<ActionResult<ApiResponse>> MarkAsRead(int id, CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var userId = User.GetUserId();
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            var ok = await _notificationService.MarkAsReadAsync(tenantId.Value, userId, id, ct);
            if (!ok)
                return NotFound(new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.NotFound, Message = "اعلان یافت نشد." });

            return Ok(new ApiResponse
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "اعلان به عنوان خوانده‌شده ثبت شد."
            });
        }

        [HttpPost("MarkAllAsRead")]
        public async Task<ActionResult<ApiResponse<int>>> MarkAllAsRead(CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var userId = User.GetUserId();
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new ApiResponse<int> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            var count = await _notificationService.MarkAllAsReadAsync(tenantId.Value, userId, ct);
            return Ok(new ApiResponse<int>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "همه اعلان‌ها خوانده شدند.",
                Data = count
            });
        }
    }
}
