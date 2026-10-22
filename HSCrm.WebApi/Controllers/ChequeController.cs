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

        public ChequeController(
            IUnitOfWork unitOfWork,
            IAccountingService accountingService,
            ICurrentFiscalYearService currentFiscalYear)
        {
            _unitOfWork = unitOfWork;
            _accountingService = accountingService;
            _currentFiscalYear = currentFiscalYear;
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
            var query = _unitOfWork.chequeUW.Get(
                x => !x.IsDeleted
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
            if (!fiscalYearId.HasValue)
                return BadRequest(new ApiResponse { Status = false, Message = "سال مالی فعال انتخاب نشده است." });

            if (model.Amount <= 0)
                return BadRequest(new ApiResponse { Status = false, Message = "مبلغ چک باید بزرگتر از صفر باشد." });

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
                return BadRequest(new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "خطا در ثبت چک: " + ex.Message
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
            using var transaction = await _unitOfWork.BeginTransactionAsync(ct);
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
                return BadRequest(new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "خطا در تغییر وضعیت چک: " + ex.Message
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
