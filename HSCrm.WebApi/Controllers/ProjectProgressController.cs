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
    public class ProjectProgressController : ControllerBase
    {
        private const long MaxUploadFileSizeBytes = 10 * 1024 * 1024;
        private readonly IProjectProgressService _progressService;
        private readonly ICurrentFiscalYearService _currentFiscalYear;
        private readonly IPermissionService _permissionService;
        private readonly IWebHostEnvironment? _hosting;

        public ProjectProgressController(
            IProjectProgressService progressService,
            ICurrentFiscalYearService currentFiscalYear,
            IPermissionService permissionService,
            IWebHostEnvironment? hosting = null)
        {
            _progressService = progressService;
            _currentFiscalYear = currentFiscalYear;
            _permissionService = permissionService;
            _hosting = hosting;
        }

        [HttpGet("Milestones/{projectId:int}")]
        [Permission(Permissions.ProjectProgress.View)]
        public async Task<ActionResult<ApiResponse<List<ProjectMilestoneDto>>>> GetMilestones(int projectId, CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var actorId = User.GetUserId();
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(actorId))
                return Unauthorized(new ApiResponse<List<ProjectMilestoneDto>> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            try
            {
                var list = await _progressService.GetMilestonesAsync(tenantId.Value, actorId, projectId, ct);
                return Ok(new ApiResponse<List<ProjectMilestoneDto>>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "فهرست مراحل پروژه دریافت شد.",
                    Data = list
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse<List<ProjectMilestoneDto>> { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<List<ProjectMilestoneDto>> { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = ex.Message });
            }
        }

        [HttpPost("CreateMilestone")]
        [Permission(Permissions.ProjectProgress.Create)]
        public async Task<ActionResult<ApiResponse<ProjectMilestoneDto>>> CreateMilestone([FromBody] ProjectMilestoneCreateModel model, CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var actorId = User.GetUserId();
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(actorId))
                return Unauthorized(new ApiResponse<ProjectMilestoneDto> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            try
            {
                var created = await _progressService.CreateMilestoneAsync(tenantId.Value, actorId, model, ct);
                return Ok(new ApiResponse<ProjectMilestoneDto>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "مرحله (Milestone) با موفقیت ثبت شد.",
                    Data = created
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse<ProjectMilestoneDto> { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<ProjectMilestoneDto> { Status = false, StatusCode = ApiResultStatusCode.NotFound, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<ProjectMilestoneDto> { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = ex.Message });
            }
        }

        [HttpPut("UpdateMilestone")]
        [Permission(Permissions.ProjectProgress.Edit)]
        public async Task<ActionResult<ApiResponse<ProjectMilestoneDto>>> UpdateMilestone([FromBody] ProjectMilestoneUpdateModel model, CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var actorId = User.GetUserId();
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(actorId))
                return Unauthorized(new ApiResponse<ProjectMilestoneDto> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            try
            {
                var updated = await _progressService.UpdateMilestoneAsync(tenantId.Value, actorId, model, ct);
                return Ok(new ApiResponse<ProjectMilestoneDto>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "مرحله پروژه با موفقیت بروزرسانی شد.",
                    Data = updated
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse<ProjectMilestoneDto> { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<ProjectMilestoneDto> { Status = false, StatusCode = ApiResultStatusCode.NotFound, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<ProjectMilestoneDto> { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = ex.Message });
            }
        }

        [HttpDelete("DeleteMilestone/{id:int}")]
        [Permission(Permissions.ProjectProgress.Delete)]
        public async Task<ActionResult<ApiResponse>> DeleteMilestone(int id, CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var actorId = User.GetUserId();
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(actorId))
                return Unauthorized(new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            try
            {
                await _progressService.DeleteMilestoneAsync(tenantId.Value, actorId, id, ct);
                return Ok(new ApiResponse
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "مرحله پروژه با موفقیت حذف شد."
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.NotFound, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = ex.Message });
            }
        }

        [HttpGet("Timeline/{projectId:int}")]
        [Permission(Permissions.ProjectProgress.View)]
        public async Task<ActionResult<ApiResponse<List<ProjectProgressLogDto>>>> GetTimeline(
            int projectId,
            [FromQuery] int? fiscalYearId = null,
            CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var actorId = User.GetUserId();
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(actorId))
                return Unauthorized(new ApiResponse<List<ProjectProgressLogDto>> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            try
            {
                var list = await _progressService.GetTimelineAsync(tenantId.Value, actorId, projectId, fiscalYearId, ct);
                return Ok(new ApiResponse<List<ProjectProgressLogDto>>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "خط زمانی پیشرفت پروژه دریافت شد.",
                    Data = list
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse<List<ProjectProgressLogDto>> { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<List<ProjectProgressLogDto>> { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = ex.Message });
            }
        }

        [HttpGet("Gallery/{projectId:int}")]
        [Permission(Permissions.ProjectProgress.View)]
        public async Task<ActionResult<ApiResponse<List<ProjectAttachmentDto>>>> GetGallery(int projectId, CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var actorId = User.GetUserId();
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(actorId))
                return Unauthorized(new ApiResponse<List<ProjectAttachmentDto>> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            try
            {
                var list = await _progressService.GetGalleryAsync(tenantId.Value, actorId, projectId, ct);
                return Ok(new ApiResponse<List<ProjectAttachmentDto>>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "گالری مستندات کارگاهی دریافت شد.",
                    Data = list
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse<List<ProjectAttachmentDto>> { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<List<ProjectAttachmentDto>> { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = ex.Message });
            }
        }

        [HttpPost("AddProgressLog")]
        [Permission(Permissions.ProjectProgress.Create)]
        [RequestSizeLimit(50 * 1024 * 1024)]
        public async Task<ActionResult<ApiResponse<ProjectProgressLogDto>>> AddProgressLog([FromForm] ProjectProgressLogFormModel form, CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var actorId = User.GetUserId();
            var fyId = _currentFiscalYear.FiscalYearId;
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(actorId) || !fyId.HasValue)
                return Unauthorized(new ApiResponse<ProjectProgressLogDto> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی یا سال مالی نامعتبر" });

            try
            {
                var uploads = new List<ProjectAttachmentUpload>();
                if (form.Files != null && form.Files.Count > 0)
                {
                    foreach (var file in form.Files)
                    {
                        if (file == null || file.Length <= 0)
                            continue;
                        if (file.Length > MaxUploadFileSizeBytes)
                            return BadRequest(new ApiResponse<ProjectProgressLogDto> { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = $"حجم فایل «{file.FileName}» نباید بیشتر از ۱۰ مگابایت باشد." });

                        using var ms = new MemoryStream();
                        await file.CopyToAsync(ms, ct);
                        uploads.Add(new ProjectAttachmentUpload
                        {
                            FileName = file.FileName,
                            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                            Length = file.Length,
                            Content = ms.ToArray()
                        });
                    }
                }

                var input = new ProjectProgressLogCreateInput
                {
                    ProjectId = form.ProjectId,
                    MilestoneId = form.MilestoneId,
                    LogDate = form.LogDate ?? string.Empty,
                    Title = form.Title ?? string.Empty,
                    ReportText = form.ReportText ?? string.Empty,
                    ProgressPercent = form.ProgressPercent,
                    WorkshopArea = form.WorkshopArea,
                    Files = uploads
                };

                var webRoot = _hosting?.WebRootPath
                    ?? (_hosting != null ? Path.Combine(_hosting.ContentRootPath, "wwwroot") : Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"));

                var created = await _progressService.AddProgressLogAsync(tenantId.Value, fyId.Value, actorId, input, webRoot, ct);
                return Ok(new ApiResponse<ProjectProgressLogDto>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "گزارش پیشرفت و مستندات کارگاهی با موفقیت ثبت شد.",
                    Data = created
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse<ProjectProgressLogDto> { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<ProjectProgressLogDto> { Status = false, StatusCode = ApiResultStatusCode.NotFound, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<ProjectProgressLogDto> { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = ex.Message });
            }
        }

        [HttpDelete("DeleteProgressLog/{id:int}")]
        [Permission(Permissions.ProjectProgress.Delete)]
        public async Task<ActionResult<ApiResponse>> DeleteProgressLog(int id, CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var actorId = User.GetUserId();
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(actorId))
                return Unauthorized(new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            try
            {
                await _progressService.DeleteProgressLogAsync(tenantId.Value, actorId, id, ct);
                return Ok(new ApiResponse
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "گزارش پیشرفت حذف شد."
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.NotFound, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = ex.Message });
            }
        }

        [HttpGet("ProfitAndLoss/{projectId:int}")]
        public async Task<ActionResult<ApiResponse<ProjectProfitAndLossDto>>> GetProfitAndLoss(
            int projectId,
            [FromQuery] int? fiscalYearId = null,
            CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var actorId = User.GetUserId();
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(actorId))
                return Unauthorized(new ApiResponse<ProjectProfitAndLossDto> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            var canView = await _permissionService.HasPermissionAsync(actorId, Permissions.Projects.View)
                          || await _permissionService.HasPermissionAsync(actorId, Permissions.AccountingReports.View)
                          || await _permissionService.HasPermissionAsync(actorId, Permissions.ProjectProgress.View);
            if (!canView)
                return StatusCode(403, new ApiResponse<ProjectProfitAndLossDto> { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = "مجوز مشاهده گزارش سود و زیان پروژه را ندارید." });

            try
            {
                var dto = await _progressService.GetProjectProfitAndLossAsync(tenantId.Value, actorId, projectId, fiscalYearId, ct);
                return Ok(new ApiResponse<ProjectProfitAndLossDto>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "گزارش سود و زیان پروژه محاسبه شد.",
                    Data = dto
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse<ProjectProfitAndLossDto> { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<ProjectProfitAndLossDto> { Status = false, StatusCode = ApiResultStatusCode.NotFound, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<ProjectProfitAndLossDto> { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = ex.Message });
            }
        }
    }

    public class ProjectProgressLogFormModel
    {
        public int ProjectId { get; set; }
        public int? MilestoneId { get; set; }
        public string? LogDate { get; set; }
        public string? Title { get; set; }
        public string? ReportText { get; set; }
        public decimal ProgressPercent { get; set; }
        public string? WorkshopArea { get; set; }
        public List<IFormFile>? Files { get; set; }
    }
}
