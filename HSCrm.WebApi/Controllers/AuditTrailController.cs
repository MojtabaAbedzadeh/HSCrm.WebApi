using System.Globalization;
using Common.Permissions;
using Framework.Authorization;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Models;
using HSCrm.Framework.Extensions;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SinaPharm.Common;

namespace HSCrm.WebApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AuditTrailController : ControllerBase
    {
        private readonly IAuditTrailService _auditTrailService;

        public AuditTrailController(IAuditTrailService auditTrailService)
        {
            _auditTrailService = auditTrailService;
        }

        [HttpGet("GetLogs")]
        [Permission(Permissions.AuditTrail.View)]
        public async Task<ActionResult<ApiResponse<AuditTrailQueryResultDto>>> GetLogs(
            [FromQuery] string? entityType = null,
            [FromQuery] string? userId = null,
            [FromQuery] string? search = null,
            [FromQuery] string? fromDate = null,
            [FromQuery] string? toDate = null,
            [FromQuery] bool includeDeleted = true,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            if (!tenantId.HasValue)
                return Unauthorized(new ApiResponse<AuditTrailQueryResultDto> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            var from = ParseOptionalDate(fromDate);
            var to = ParseOptionalDate(toDate);

            var data = await _auditTrailService.GetAuditLogsAsync(
                tenantId.Value,
                entityType,
                userId,
                search,
                from,
                to,
                includeDeleted,
                page,
                pageSize,
                ct);

            return Ok(new ApiResponse<AuditTrailQueryResultDto>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "تاریخچه تغییرات اسناد دریافت شد.",
                Data = data
            });
        }

        [HttpGet("EntityHistory")]
        [Permission(Permissions.AuditTrail.View)]
        public async Task<ActionResult<ApiResponse<AuditTrailItemDto>>> GetEntityHistory(
            [FromQuery] string entityType,
            [FromQuery] int entityId,
            CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            if (!tenantId.HasValue)
                return Unauthorized(new ApiResponse<AuditTrailItemDto> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            if (string.IsNullOrWhiteSpace(entityType) || entityId <= 0)
                return BadRequest(new ApiResponse<AuditTrailItemDto> { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = "نوع موجودیت و شناسه معتبر الزامی است." });

            var item = await _auditTrailService.GetEntityHistoryAsync(tenantId.Value, entityType, entityId, ct);
            if (item == null)
                return NotFound(new ApiResponse<AuditTrailItemDto> { Status = false, StatusCode = ApiResultStatusCode.NotFound, Message = "سابقه تغییر یافت نشد." });

            return Ok(new ApiResponse<AuditTrailItemDto>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "سابقه ثبت و ویرایش دریافت شد.",
                Data = item
            });
        }

        private static DateTime? ParseOptionalDate(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;
            var s = raw.Trim();
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) && parsed.Year >= 1900)
                return parsed;
            try
            {
                return ConvertDate.ConvertShamsiToMiladi(s);
            }
            catch
            {
                return null;
            }
        }
    }
}
