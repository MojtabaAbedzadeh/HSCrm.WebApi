using Common.Permissions;
using Framework.Authorization;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Framework.Extensions;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SinaPharm.Common;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.WebApi.Controllers
{
    /// <summary>
    /// کنترلر مدیریت چک‌های دریافتنی و پرداختنی، واگذاری به بانک (کلکسیون)، وصول، برگشت چک و ماشین‌حساب راس‌گیری
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ChequeController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAccountingService _accountingService;
        private readonly ICurrentFiscalYearService _currentFiscalYear;
        private readonly ILogger<ChequeController> _logger;

        public ChequeController(
            IUnitOfWork unitOfWork,
            IAccountingService accountingService,
            ICurrentFiscalYearService currentFiscalYear,
            ILogger<ChequeController> logger)
        {
            _unitOfWork = unitOfWork;
            _accountingService = accountingService;
            _currentFiscalYear = currentFiscalYear;
            _logger = logger;
        }

        /// <summary>
        /// دریافت لیست چک‌های دریافتنی و پرداختنی با قابلیت فیلتر وضعیت، مشتری، تأمین‌کننده و حساب بانکی
        /// </summary>
        [HttpGet("GetAll")]
        [HttpGet("GetCheques")]
        [Permission(Permissions.Cheques.View)]
        public async Task<ActionResult<ApiResponse<List<Cheque>>>> GetAll(
            [FromQuery] ChequeType? type,
            [FromQuery] ChequeStatus? status,
            [FromQuery] int? customerId,
            [FromQuery] int? supplierId,
            [FromQuery] int? bankAccountId,
            CancellationToken ct)
        {
            var tenantId = User.GetTenantId();
            var fiscalYearId = _currentFiscalYear.FiscalYearId;
            if (!tenantId.HasValue)
                return Unauthorized(new ApiResponse<List<Cheque>>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Unauthorized,
                    Message = "دسترسی نامعتبر"
                });

            if (!fiscalYearId.HasValue)
                return BadRequest(new ApiResponse<List<Cheque>>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "سال مالی فعال انتخاب نشده است."
                });

            var query = _unitOfWork.chequeUW.Get(
                x => x.TenantId == tenantId.Value && x.FiscalYearId == fiscalYearId.Value && !x.IsDeleted
                     && (!type.HasValue || x.Type == type.Value)
                     && (!status.HasValue || x.Status == status.Value)
                     && (!customerId.HasValue || x.CustomerId == customerId.Value)
                     && (!supplierId.HasValue || x.SupplierId == supplierId.Value)
                     && (!bankAccountId.HasValue || x.BankAccountId == bankAccountId.Value),
                asNoTracking: true,
                x => x.Customer!,
                x => x.Supplier!,
                x => x.BankAccount!,
                x => x.Project!);

            var list = await query
                .OrderBy(x => x.DueDate)
                .ThenByDescending(x => x.Id)
                .ToListAsync(ct);

            return Ok(new ApiResponse<List<Cheque>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "لیست چک‌ها با موفقیت دریافت شد.",
                Data = list
            });
        }

        /// <summary>
        /// ثبت چک دریافتنی (از مشتری) یا چک پرداختنی (صادره شرکت) + صدور خودکار سند حسابداری
        /// </summary>
        [HttpPost("Create")]
        [Permission(Permissions.Cheques.Create)]
        public async Task<ActionResult<ApiResponse<Cheque>>> Create(
            [FromBody] ChequeCreateModel model,
            CancellationToken ct)
        {
            var tenantId = User.GetTenantId();
            if (!tenantId.HasValue)
                return Unauthorized(new ApiResponse { Status = false, Message = "دسترسی نامعتبر" });

            var fiscalYearId = _currentFiscalYear.FiscalYearId;
            if (!fiscalYearId.HasValue
                || !await _unitOfWork.fiscalYearUW.Get(
                    x => x.Id == fiscalYearId.Value && x.TenantId == tenantId.Value && !x.IsClosed && !x.IsDeleted,
                    asNoTracking: true).AnyAsync(ct))
                return BadRequest(new ApiResponse { Status = false, Message = "سال مالی فعال و معتبر برای شرکت جاری انتخاب نشده است." });

            if (model == null
                || string.IsNullOrWhiteSpace(model.ChequeNumber)
                || string.IsNullOrWhiteSpace(model.BankName)
                || model.Amount <= 0)
                return BadRequest(new ApiResponse { Status = false, Message = "مبلغ چک باید بزرگتر از صفر باشد." });

            if (!Enum.IsDefined(model.Type))
                return BadRequest(new ApiResponse { Status = false, Message = "نوع چک معتبر نیست." });

            if (model.Type == ChequeType.Receivable)
            {
                if (!model.CustomerId.HasValue || model.SupplierId.HasValue)
                    return BadRequest(new ApiResponse { Status = false, Message = "چک دریافتنی باید به یک مشتری متعلق باشد و تأمین‌کننده نداشته باشد." });

                var customerExists = await _unitOfWork.customerUW
                    .Get(x => x.Id == model.CustomerId.Value && x.TenantId == tenantId.Value && !x.IsDeleted, asNoTracking: true)
                    .AnyAsync(ct);
                if (!customerExists)
                    return BadRequest(new ApiResponse { Status = false, Message = "مشتری انتخاب‌شده در شرکت جاری وجود ندارد." });

                if (model.BankAccountId.HasValue)
                {
                    var bankExists = await _unitOfWork.bankAccountUW
                        .Get(x => x.Id == model.BankAccountId.Value && x.TenantId == tenantId.Value && !x.IsDeleted && x.IsActive, asNoTracking: true)
                        .AnyAsync(ct);
                    if (!bankExists)
                        return BadRequest(new ApiResponse { Status = false, Message = "حساب بانکی انتخاب‌شده معتبر یا فعال نیست." });
                }
            }
            else if (model.Type == ChequeType.Payable)
            {
                if (!model.SupplierId.HasValue || model.CustomerId.HasValue || !model.BankAccountId.HasValue)
                    return BadRequest(new ApiResponse { Status = false, Message = "چک پرداختنی باید به یک تأمین‌کننده و حساب بانکی شرکت متصل باشد." });

                var supplierExists = await _unitOfWork.supplierUW
                    .Get(x => x.Id == model.SupplierId.Value && x.TenantId == tenantId.Value && !x.IsDeleted, asNoTracking: true)
                    .AnyAsync(ct);
                if (!supplierExists)
                    return BadRequest(new ApiResponse { Status = false, Message = "تأمین‌کننده انتخاب‌شده در شرکت جاری وجود ندارد." });

                var bankExists = await _unitOfWork.bankAccountUW
                    .Get(x => x.Id == model.BankAccountId.Value && x.TenantId == tenantId.Value && !x.IsDeleted && x.IsActive, asNoTracking: true)
                    .AnyAsync(ct);
                if (!bankExists)
                    return BadRequest(new ApiResponse { Status = false, Message = "حساب بانکی انتخاب‌شده معتبر یا فعال نیست." });
            }

            if (model.ProjectId.HasValue)
            {
                var projectExists = await _unitOfWork.projectUW
                    .Get(x => x.Id == model.ProjectId.Value && x.TenantId == tenantId.Value && !x.IsDeleted
                        && (model.Type != ChequeType.Receivable || x.CustomerId == model.CustomerId),
                        asNoTracking: true)
                    .AnyAsync(ct);
                if (!projectExists)
                    return BadRequest(new ApiResponse { Status = false, Message = "پروژه انتخاب‌شده متعلق به شرکت یا مشتری انتخاب‌شده نیست." });
            }

            using var transaction = await _unitOfWork.BeginTransactionAsync(ct);
            try
            {
                var cheque = new Cheque
                {
                    TenantId = tenantId.Value,
                    FiscalYearId = fiscalYearId.Value,
                    Type = model.Type,
                    Status = ChequeStatus.InSafe,
                    ChequeNumber = model.ChequeNumber.Trim(),
                    SayadiId = model.SayadiId?.Trim(),
                    Amount = model.Amount,
                    IssueDate = ConvertDate.ConvertShamsiToMiladi(model.IssueDateShamsi),
                    DueDate = ConvertDate.ConvertShamsiToMiladi(model.DueDateShamsi),
                    BankName = model.BankName.Trim(),
                    BranchName = model.BranchName?.Trim(),
                    DrawerName = model.DrawerName?.Trim(),
                    CustomerId = model.CustomerId,
                    SupplierId = model.SupplierId,
                    ProjectId = model.ProjectId,
                    BankAccountId = model.BankAccountId,
                    Description = model.Description?.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = User.GetUserId()
                };

                await _unitOfWork.chequeUW.CreateAsync(cheque, ct);
                await _unitOfWork.SaveAsync();

                await _accountingService.CreateVoucherForChequeRegistrationAsync(cheque, ct);

                await transaction.CommitAsync(ct);

                return Ok(new ApiResponse<Cheque>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = cheque.Type == ChequeType.Receivable
                        ? "چک دریافتنی با موفقیت ثبت شد و سند اسناد دریافتنی نزد صندوق صادر گردید."
                        : "چک پرداختنی با موفقیت ثبت شد و سند اسناد پرداختنی تجاری صادر گردید.",
                    Data = cheque
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to register cheque for tenant {TenantId} and fiscal year {FiscalYearId}.", tenantId.Value, fiscalYearId.Value);
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.ServerError,
                    Message = "خطا در ثبت چک رخ داد. جزئیات در گزارش سرور ثبت شده است."
                });
            }
        }

        /// <summary>
        /// تغییر وضعیت چک (واگذاری به بانک جهت وصول، وصول نهایی، برگشت چک، خرج چک به تأمین‌کننده، پاس شدن چک پرداختنی) + ثبت سند حسابداری خودکار
        /// </summary>
        [HttpPost("ChangeStatus")]
        [Permission(Permissions.Cheques.ChangeStatus)]
        public async Task<ActionResult<ApiResponse<JournalVoucher>>> ChangeStatus(
            [FromBody] ChequeStatusChangeModel model,
            CancellationToken ct)
        {
            // Serialize status changes for the same cheque to prevent concurrent
            // requests from posting duplicate accounting vouchers.
            using var transaction = _unitOfWork.BeginTransaction(System.Data.IsolationLevel.Serializable);
            try
            {
                var voucher = await _accountingService.ChangeChequeStatusAndCreateVoucherAsync(model, ct);
                await transaction.CommitAsync(ct);

                return Ok(new ApiResponse<JournalVoucher>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = $"تغییر وضعیت چک با موفقیت انجام شد و سند حسابداری شماره {voucher.VoucherNumber} صادر گردید.",
                    Data = voucher
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to change cheque status for cheque {ChequeId}.", model.ChequeId);
                return BadRequest(new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "تغییر وضعیت چک انجام نشد. اطلاعات ورودی یا وضعیت فعلی چک را بررسی کنید."
                });
            }
        }

        /// <summary>
        /// محاسبه راس‌گیری چک‌ها (میانگین موزون زمانی و ریالی سررسید چک‌ها نسبت به تاریخ مبنا + ارزش فعلی و فرجه)
        /// </summary>
        [HttpPost("RasGiri")]
        [HttpPost("CalculateRasGiri")]
        [Permission(Permissions.Cheques.View)]
        public async Task<ActionResult<ApiResponse<ChequeRasGiriResultDto>>> CalculateRasGiri(
            [FromBody] ChequeRasGiriRequestModel model,
            CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.CalculateChequesRasGiriAsync(model, ct);
                return Ok(new ApiResponse<ChequeRasGiriResultDto>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = $"راس چک‌ها با موفقیت محاسبه شد: {result.WeightedAverageDays} روز (تاریخ راس: {result.ExactRasDateShamsi})",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cheque ras-giri calculation failed.");
                return BadRequest(new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "محاسبه راس‌گیری انجام نشد. فیلترها و تاریخ‌های ورودی را بررسی کنید."
                });
            }
        }
    }
}
