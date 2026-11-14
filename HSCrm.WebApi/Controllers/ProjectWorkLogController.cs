using Common.Permissions;
using Framework.Authorization;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Models;
using HSCrm.Framework.Extensions;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.WebApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectWorkLogController : ControllerBase
    {
        private readonly IProjectTeamService _teamService;
        private readonly ICurrentFiscalYearService _currentFiscalYear;

        public ProjectWorkLogController(IProjectTeamService teamService, ICurrentFiscalYearService currentFiscalYear)
        {
            _teamService = teamService;
            _currentFiscalYear = currentFiscalYear;
        }

        [HttpGet("GetEmployeeDashboard")]
        public async Task<ActionResult<ApiResponse<EmployeeDashboardDto>>> GetEmployeeDashboard(CancellationToken ct)
        {
            var tenantId = User.GetTenantId();
            var userId = User.GetUserId();
            var fyId = _currentFiscalYear.FiscalYearId;
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(userId) || !fyId.HasValue)
                return Unauthorized(new ApiResponse<EmployeeDashboardDto> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی یا سال مالی نامعتبر" });

            var dto = await _teamService.GetEmployeeDashboardAsync(tenantId.Value, fyId.Value, userId, ct);
            return Ok(new ApiResponse<EmployeeDashboardDto>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "اطلاعات کارتابل کارمند دریافت شد.",
                Data = dto
            });
        }

        [HttpGet("GetWorkLogs")]
        [Permission(Permissions.WorkLogs.View)]
        public async Task<ActionResult<ApiResponse<List<ProjectWorkLogDto>>>> GetWorkLogs(
            [FromQuery] int? projectId = null,
            [FromQuery] string? userId = null,
            [FromQuery] WorkLogStatus? status = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var actorId = User.GetUserId();
            var fyId = _currentFiscalYear.FiscalYearId;
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(actorId) || !fyId.HasValue)
                return Unauthorized(new ApiResponse<List<ProjectWorkLogDto>> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی یا سال مالی نامعتبر" });

            try
            {
                var list = await _teamService.GetWorkLogsAsync(tenantId.Value, fyId.Value, actorId, projectId, userId, status, fromDate, toDate, ct);
                return Ok(new ApiResponse<List<ProjectWorkLogDto>>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "لیست ساعات کاری دریافت شد.",
                    Data = list
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse<List<ProjectWorkLogDto>> { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = ex.Message });
            }
        }

        [HttpPost("CreateWorkLog")]
        [Permission(Permissions.WorkLogs.Create)]
        public async Task<ActionResult<ApiResponse<ProjectWorkLogDto>>> CreateWorkLog([FromBody] ProjectWorkLogCreateModel model, CancellationToken ct)
        {
            var tenantId = User.GetTenantId();
            var actorId = User.GetUserId();
            var fyId = _currentFiscalYear.FiscalYearId;
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(actorId) || !fyId.HasValue)
                return Unauthorized(new ApiResponse<ProjectWorkLogDto> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی یا سال مالی نامعتبر" });

            try
            {
                var created = await _teamService.CreateWorkLogAsync(tenantId.Value, fyId.Value, actorId, model, ct);
                return Ok(new ApiResponse<ProjectWorkLogDto>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "ساعت کار با موفقیت ثبت و برای تأیید ارسال شد.",
                    Data = created
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse<ProjectWorkLogDto> { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<ProjectWorkLogDto> { Status = false, StatusCode = ApiResultStatusCode.NotFound, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<ProjectWorkLogDto> { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = ex.Message });
            }
        }

        [HttpPut("UpdateWorkLog")]
        [Permission(Permissions.WorkLogs.Edit)]
        public async Task<ActionResult<ApiResponse<ProjectWorkLogDto>>> UpdateWorkLog([FromBody] ProjectWorkLogEditModel model, CancellationToken ct)
        {
            var tenantId = User.GetTenantId();
            var actorId = User.GetUserId();
            var fyId = _currentFiscalYear.FiscalYearId;
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(actorId) || !fyId.HasValue)
                return Unauthorized(new ApiResponse<ProjectWorkLogDto> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی یا سال مالی نامعتبر" });

            try
            {
                var updated = await _teamService.UpdateWorkLogAsync(tenantId.Value, fyId.Value, actorId, model, ct);
                return Ok(new ApiResponse<ProjectWorkLogDto>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "ساعت کار با موفقیت ویرایش شد.",
                    Data = updated
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse<ProjectWorkLogDto> { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<ProjectWorkLogDto> { Status = false, StatusCode = ApiResultStatusCode.NotFound, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<ProjectWorkLogDto> { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = ex.Message });
            }
        }

        [HttpDelete("DeleteWorkLog/{id:int}")]
        [Permission(Permissions.WorkLogs.Delete)]
        public async Task<ActionResult<ApiResponse>> DeleteWorkLog(int id, CancellationToken ct)
        {
            var tenantId = User.GetTenantId();
            var actorId = User.GetUserId();
            var fyId = _currentFiscalYear.FiscalYearId;
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(actorId) || !fyId.HasValue)
                return Unauthorized(new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی یا سال مالی نامعتبر" });

            try
            {
                var deleted = await _teamService.DeleteWorkLogAsync(tenantId.Value, fyId.Value, actorId, id, ct);
                if (!deleted)
                    return NotFound(new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.NotFound, Message = "رکورد یافت نشد." });

                return Ok(new ApiResponse
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "رکورد ساعت کار حذف شد."
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = ex.Message });
            }
        }

        [HttpPost("ReviewWorkLog")]
        [Permission(Permissions.WorkLogs.Approve)]
        public async Task<ActionResult<ApiResponse<ProjectWorkLogDto>>> ReviewWorkLog([FromBody] ProjectWorkLogReviewModel model, CancellationToken ct)
        {
            var tenantId = User.GetTenantId();
            var reviewerId = User.GetUserId();
            var fyId = _currentFiscalYear.FiscalYearId;
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(reviewerId) || !fyId.HasValue)
                return Unauthorized(new ApiResponse<ProjectWorkLogDto> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی یا سال مالی نامعتبر" });

            try
            {
                var updated = await _teamService.ReviewWorkLogAsync(tenantId.Value, fyId.Value, reviewerId, model, ct);
                return Ok(new ApiResponse<ProjectWorkLogDto>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = model.IsApproved ? "ساعت کار تأیید و هزینه دستمزد محاسبه شد." : "ساعت کار رد شد.",
                    Data = updated
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<ProjectWorkLogDto> { Status = false, StatusCode = ApiResultStatusCode.NotFound, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<ProjectWorkLogDto> { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = ex.Message });
            }
        }

        [HttpPost("BatchReviewWorkLogs")]
        [Permission(Permissions.WorkLogs.Approve)]
        public async Task<ActionResult<ApiResponse<object>>> BatchReviewWorkLogs([FromBody] ProjectWorkLogBatchReviewModel model, CancellationToken ct)
        {
            var tenantId = User.GetTenantId();
            var reviewerId = User.GetUserId();
            var fyId = _currentFiscalYear.FiscalYearId;
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(reviewerId) || !fyId.HasValue)
                return Unauthorized(new ApiResponse<object> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی یا سال مالی نامعتبر" });

            try
            {
                var count = await _teamService.BatchReviewWorkLogsAsync(tenantId.Value, fyId.Value, reviewerId, model, ct);
                return Ok(new ApiResponse<object>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = $"{count} مورد ساعت کار {(model.IsApproved ? "تأیید" : "رد")} شد.",
                    Data = new { updatedCount = count }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object> { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = ex.Message });
            }
        }
    }
}
