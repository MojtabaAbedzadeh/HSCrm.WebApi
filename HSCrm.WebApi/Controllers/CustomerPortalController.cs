using System.Globalization;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Models;
using HSCrm.Framework.Extensions;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SinaPharm.Common;

namespace HSCrm.WebApi.Controllers
{
    [Authorize(Roles = "Customer")]
    [Route("api/[controller]")]
    [ApiController]
    public class CustomerPortalController : ControllerBase
    {
        private readonly ICustomerPortalService _portalService;
        private readonly ICurrentFiscalYearService? _currentFiscalYear;

        public CustomerPortalController(
            ICustomerPortalService portalService,
            ICurrentFiscalYearService? currentFiscalYear = null)
        {
            _portalService = portalService;
            _currentFiscalYear = currentFiscalYear;
        }

        [HttpGet("Dashboard")]
        public async Task<ActionResult<ApiResponse<CustomerPortalDashboardDto>>> GetDashboard(CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var userId = User.GetUserId();
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new ApiResponse<CustomerPortalDashboardDto> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            try
            {
                var data = await _portalService.GetDashboardAsync(tenantId.Value, userId, ct);
                return Ok(new ApiResponse<CustomerPortalDashboardDto>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "اطلاعات پیشخوان مشتری دریافت شد.",
                    Data = data
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse<CustomerPortalDashboardDto> { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<CustomerPortalDashboardDto> { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = ex.Message });
            }
        }

        [HttpGet("Projects")]
        public async Task<ActionResult<ApiResponse<List<CustomerPortalProjectSummaryDto>>>> GetProjects(CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var userId = User.GetUserId();
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new ApiResponse<List<CustomerPortalProjectSummaryDto>> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            try
            {
                var data = await _portalService.GetProjectsAsync(tenantId.Value, userId, ct);
                return Ok(new ApiResponse<List<CustomerPortalProjectSummaryDto>>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "فهرست پروژه‌های مشتری دریافت شد.",
                    Data = data
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse<List<CustomerPortalProjectSummaryDto>> { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<List<CustomerPortalProjectSummaryDto>> { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = ex.Message });
            }
        }

        [HttpGet("Projects/{projectId:int}")]
        public async Task<ActionResult<ApiResponse<CustomerPortalProjectDetailDto>>> GetProjectDetail(int projectId, CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var userId = User.GetUserId();
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new ApiResponse<CustomerPortalProjectDetailDto> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            try
            {
                var data = await _portalService.GetProjectDetailAsync(tenantId.Value, userId, projectId, ct);
                return Ok(new ApiResponse<CustomerPortalProjectDetailDto>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "جزئیات پیشرفت پروژه دریافت شد.",
                    Data = data
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse<CustomerPortalProjectDetailDto> { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<CustomerPortalProjectDetailDto> { Status = false, StatusCode = ApiResultStatusCode.NotFound, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<CustomerPortalProjectDetailDto> { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = ex.Message });
            }
        }

        [HttpGet("Invoices")]
        public async Task<ActionResult<ApiResponse<List<CustomerPortalInvoiceDto>>>> GetInvoices(CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var userId = User.GetUserId();
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new ApiResponse<List<CustomerPortalInvoiceDto>> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            try
            {
                var data = await _portalService.GetInvoicesAsync(tenantId.Value, userId, ct);
                return Ok(new ApiResponse<List<CustomerPortalInvoiceDto>>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "فهرست فاکتورهای فروش دریافت شد.",
                    Data = data
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse<List<CustomerPortalInvoiceDto>> { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<List<CustomerPortalInvoiceDto>> { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = ex.Message });
            }
        }

        [HttpGet("Invoices/{invoiceId:int}")]
        public async Task<ActionResult<ApiResponse<CustomerPortalInvoiceDto>>> GetInvoiceById(int invoiceId, CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var userId = User.GetUserId();
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new ApiResponse<CustomerPortalInvoiceDto> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            try
            {
                var data = await _portalService.GetInvoiceByIdAsync(tenantId.Value, userId, invoiceId, ct);
                return Ok(new ApiResponse<CustomerPortalInvoiceDto>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "اطلاعات فاکتور فروش دریافت شد.",
                    Data = data
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse<CustomerPortalInvoiceDto> { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<CustomerPortalInvoiceDto> { Status = false, StatusCode = ApiResultStatusCode.NotFound, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<CustomerPortalInvoiceDto> { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = ex.Message });
            }
        }

        [HttpGet("Statement")]
        public async Task<ActionResult<ApiResponse<CustomerPortalStatementDto>>> GetStatement(
            [FromQuery] string? fromDate = null,
            [FromQuery] string? toDate = null,
            [FromQuery] int? fiscalYearId = null,
            CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var userId = User.GetUserId();
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new ApiResponse<CustomerPortalStatementDto> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            try
            {
                var effectiveFy = fiscalYearId ?? _currentFiscalYear?.FiscalYearId;
                var from = ParseOptionalDate(fromDate);
                var to = ParseOptionalDate(toDate);
                var data = await _portalService.GetStatementAsync(tenantId.Value, userId, from, to, effectiveFy, ct);
                return Ok(new ApiResponse<CustomerPortalStatementDto>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "صورت‌حساب ۳۶۰ درجه مشتری دریافت شد.",
                    Data = data
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse<CustomerPortalStatementDto> { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<CustomerPortalStatementDto> { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = ex.Message });
            }
        }

        [HttpGet("Profile")]
        public async Task<ActionResult<ApiResponse<CustomerPortalProfileDto>>> GetProfile(CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var userId = User.GetUserId();
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new ApiResponse<CustomerPortalProfileDto> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            try
            {
                var data = await _portalService.GetProfileAsync(tenantId.Value, userId, ct);
                return Ok(new ApiResponse<CustomerPortalProfileDto>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "پروفایل مشتری دریافت شد.",
                    Data = data
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse<CustomerPortalProfileDto> { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<CustomerPortalProfileDto> { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = ex.Message });
            }
        }

        [HttpPut("UpdateProfile")]
        public async Task<ActionResult<ApiResponse<CustomerPortalProfileDto>>> UpdateProfile([FromBody] CustomerPortalProfileUpdateModel model, CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var userId = User.GetUserId();
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new ApiResponse<CustomerPortalProfileDto> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            try
            {
                var data = await _portalService.UpdateProfileAsync(tenantId.Value, userId, model, ct);
                return Ok(new ApiResponse<CustomerPortalProfileDto>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "اطلاعات پروفایل با موفقیت ذخیره شد.",
                    Data = data
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse<CustomerPortalProfileDto> { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<CustomerPortalProfileDto> { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = ex.Message });
            }
        }

        [HttpPost("ChangePassword")]
        public async Task<ActionResult<ApiResponse>> ChangePassword([FromBody] CustomerPortalChangePasswordModel model, CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var userId = User.GetUserId();
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            try
            {
                var (succeeded, message) = await _portalService.ChangePasswordAsync(tenantId.Value, userId, model, ct);
                if (!succeeded)
                    return BadRequest(new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = message });

                return Ok(new ApiResponse
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = message
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
