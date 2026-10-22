using Common.Permissions;
using Framework.Authorization;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SinaPharm.Common;
using static HSCrm.Common.PublicTools.StringCleaner;

namespace HSCrm.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FiscalYearController : ControllerBase
    {
        private readonly IUnitOfWork _context;
        private readonly IAccountingService _accountingService;
        private readonly ICurrentUserService _currentUser;

        public FiscalYearController(
            IUnitOfWork context,
            IAccountingService accountingService,
            ICurrentUserService currentUser)
        {
            _context = context;
            _accountingService = accountingService;
            _currentUser = currentUser;
        }

        [HttpGet("GetFiscalYears")]
        [HttpGet("GetAll")]
        [Permission(Permissions.FiscalYears.View)]
        public ApiResponse<IEnumerable<FiscalYear>> Get()
        {
            var fiscalYearList = _context.fiscalYearUW.Get().OrderByDescending(f => f.Id);
            return new ApiResponse<IEnumerable<FiscalYear>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = fiscalYearList
            };
        }

        [HttpGet("FiscalYearDropdownList")]
        [AllowAnonymous] // چون قبل از لاگین نهایی فراخوانی می‌شود
        public ApiResponse FiscalYearDropdownList(int tenantId)
        {
            var fiscalYearList = _context.fiscalYearUW.Get(f => f.TenantId == tenantId)
                .OrderByDescending(f => f.Id)
                .Select(f => new DropdownDto
                {
                    DrId = f.Id,
                    DrName = f.Title
                });

            return new ApiResponse<IEnumerable<DropdownDto>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Data = fiscalYearList
            };
        }

        [HttpPost("CreateFiscalYear")]
        [HttpPost("Create")]
        [Permission(Permissions.FiscalYears.Create)]
        public async Task<ActionResult<ApiResponse<FiscalYear>>> CreateFiscalYear(
            [FromBody] FiscalYearCreateModel model,
            CancellationToken ct)
        {
            var tenantId = _currentUser.TenantId ?? 0;
            var cleanTitle = model.Title.CleanName();

            var exists = _context.fiscalYearUW.Get(f => f.TenantId == tenantId && f.Title == cleanTitle).Any();
            if (exists)
            {
                return BadRequest(new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = $"سال مالی با عنوان {cleanTitle} قبلاً ثبت شده است."
                });
            }

            if (model.IsCurrent)
            {
                var currentYears = _context.fiscalYearUW.Get(f => f.TenantId == tenantId && f.IsCurrent, asNoTracking: false).ToList();
                foreach (var cy in currentYears)
                {
                    cy.IsCurrent = false;
                }
            }

            var fy = new FiscalYear
            {
                TenantId = tenantId,
                Title = cleanTitle,
                StartDate = ConvertDate.ConvertShamsiToMiladi(model.StartDateShamsi),
                EndDate = ConvertDate.ConvertShamsiToMiladi(model.EndDateShamsi),
                IsClosed = false,
                IsCurrent = model.IsCurrent,
                CreatedAt = DateTime.UtcNow,
                CreatedById = _currentUser.UserId
            };

            await _context.fiscalYearUW.CreateAsync(fy, ct);
            await _context.SaveAsync();

            return Ok(new ApiResponse<FiscalYear>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = $"سال مالی {fy.Title} با موفقیت ایجاد شد.",
                Data = fy
            });
        }

        [HttpPut("SetActiveFiscalYear/{id:int}")]
        [HttpPost("SetActive/{id:int}")]
        [HttpPut("SetActive/{id:int}")]
        [Permission(Permissions.FiscalYears.Edit)]
        public async Task<ActionResult<ApiResponse>> SetActiveFiscalYear(int id, CancellationToken ct)
        {
            var tenantId = _currentUser.TenantId ?? 0;
            var target = await _context.fiscalYearUW.GetByIdAsync(id);
            if (target == null || target.TenantId != tenantId)
            {
                return NotFound(new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.NotFound,
                    Message = "سال مالی مورد نظر یافت نشد."
                });
            }

            var allYears = _context.fiscalYearUW.Get(f => f.TenantId == tenantId, asNoTracking: false).ToList();
            foreach (var y in allYears)
            {
                y.IsCurrent = (y.Id == id);
            }

            await _context.SaveAsync();

            return Ok(new ApiResponse
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = $"سال مالی {target.Title} به عنوان سال مالی پیش‌فرض انتخاب شد."
            });
        }

        /// <summary>
        /// ممیزی و پیش‌نمایش کنترلی قبل از بستن سال مالی (لیست حساب‌های موقت، سود و زیان خالص، حساب‌های دائم و هشدارهای کنترلی)
        /// </summary>
        [HttpGet("PreviewFiscalYearClosing")]
        [HttpGet("PreviewClose")]
        [Permission(Permissions.FiscalYears.View)]
        public async Task<ActionResult<ApiResponse<FiscalYearClosingPreviewDto>>> PreviewFiscalYearClosing(
            [FromQuery] int? fiscalYearId,
            CancellationToken ct)
        {
            try
            {
                var preview = await _accountingService.GetFiscalYearClosingPreviewAsync(fiscalYearId, ct);
                return Ok(new ApiResponse<FiscalYearClosingPreviewDto>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                    Data = preview
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = ex.Message
                });
            }
        }

        /// <summary>
        /// اجرای عملیات پایان سال مالی: بستن حساب‌های موقت، انتقال سود/زیان به سرمایه، صدور سند اختتامیه و صدور سند افتتاحیه در سال جدید
        /// </summary>
        [HttpPost("CloseFiscalYearAndGenerateOpening")]
        [HttpPost("CloseAndOpenNext")]
        [Permission(Permissions.FiscalYears.Close)]
        public async Task<ActionResult<ApiResponse<CloseFiscalYearResultDto>>> CloseFiscalYearAndGenerateOpening(
            [FromBody] CloseFiscalYearRequestModel model,
            CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.CloseFiscalYearAndGenerateOpeningAsync(model, ct);
                return Ok(new ApiResponse<CloseFiscalYearResultDto>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = $"سال مالی {result.ClosedFiscalYearTitle} با موفقیت بسته شد و سند افتتاحیه در سال مالی {result.NewFiscalYearTitle} صادر گردید.",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = ex.Message
                });
            }
        }

        /// <summary>
        /// بازگشایی سال مالی بسته شده و حذف خودکار اسناد اختتامیه و افتتاحیه جهت انجام اصلاحات
        /// </summary>
        [HttpPost("ReopenFiscalYear/{id:int}")]
        [HttpPost("Reopen/{id:int}")]
        [Permission(Permissions.FiscalYears.Reopen)]
        public async Task<ActionResult<ApiResponse<ReopenFiscalYearResultDto>>> ReopenFiscalYear(
            int id,
            [FromQuery] int? nextFiscalYearId,
            CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.ReopenFiscalYearAsync(id, nextFiscalYearId, ct);
                return Ok(new ApiResponse<ReopenFiscalYearResultDto>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = $"سال مالی {result.ReopenedFiscalYearTitle} با موفقیت بازگشایی شد و اسناد اختتامیه/افتتاحیه مرتبط ابطال گردیدند.",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = ex.Message
                });
            }
        }
    }
}
