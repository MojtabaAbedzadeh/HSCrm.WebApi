using Common.Permissions;
using Framework.Authorization;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Framework.Extensions;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCrm.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountingController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAccountingService _accountingService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ICurrentFiscalYearService _currentFiscalYearService;

        public AccountingController(
            IUnitOfWork unitOfWork,
            IAccountingService accountingService,
            ICurrentUserService currentUserService,
            ICurrentFiscalYearService currentFiscalYearService)
        {
            _unitOfWork = unitOfWork;
            _accountingService = accountingService;
            _currentUserService = currentUserService;
            _currentFiscalYearService = currentFiscalYearService;
        }

        [HttpGet("ChartOfAccounts")]
        [Permission(Permissions.AccountingAccounts.View)]
        public async Task<ActionResult<ApiResponse<IEnumerable<AccountingAccount>>>> GetChartOfAccounts(CancellationToken ct)
        {
            var tenantId = User.GetTenantId();
            if (!tenantId.HasValue)
                return Unauthorized(new ApiResponse { Status = false, Message = "شناسه شرکت معتبر نیست" });

            await _accountingService.EnsureDefaultChartOfAccountsAsync(tenantId.Value, ct);

            var accounts = await _unitOfWork.accountingAccountUW
                .Get(x => x.TenantId == tenantId.Value && !x.IsDeleted, asNoTracking: true)
                .OrderBy(x => x.Code)
                .ToListAsync(ct);

            return Ok(new ApiResponse<IEnumerable<AccountingAccount>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "درختواره حساب‌ها با موفقیت دریافت شد",
                Data = accounts
            });
        }

        [HttpPost("CreateAccount")]
        [Permission(Permissions.AccountingAccounts.Create)]
        public async Task<ActionResult<ApiResponse<AccountingAccount>>> CreateAccount([FromBody] AccountingAccountCreateModel model, CancellationToken ct)
        {
            var tenantId = User.GetTenantId();
            if (!tenantId.HasValue)
                return Unauthorized(new ApiResponse { Status = false, Message = "دسترسی نامعتبر" });

            var account = new AccountingAccount
            {
                TenantId = tenantId.Value,
                Code = model.Code.Trim(),
                Title = model.Title.Trim(),
                Level = model.Level,
                Category = model.Category,
                Nature = model.Nature,
                ParentId = model.ParentId,
                Description = model.Description,
                IsSystemAccount = false,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            await _unitOfWork.accountingAccountUW.CreateAsync(account, ct);
            await _unitOfWork.SaveAsync();

            return Ok(new ApiResponse<AccountingAccount>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "سرفصل حسابداری با موفقیت ایجاد شد",
                Data = account
            });
        }

        [HttpGet("Vouchers")]
        [Permission(Permissions.JournalVouchers.View)]
        public async Task<ActionResult<ApiResponse<IEnumerable<JournalVoucher>>>> GetVouchers(CancellationToken ct)
        {
            var vouchers = await _unitOfWork.journalVoucherUW
                .Get(x => !x.IsDeleted, asNoTracking: true, x => x.Items)
                .OrderByDescending(x => x.VoucherDate)
                .ThenByDescending(x => x.VoucherNumber)
                .ToListAsync(ct);

            return Ok(new ApiResponse<IEnumerable<JournalVoucher>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "لیست اسناد حسابداری دریافت شد",
                Data = vouchers
            });
        }

        [HttpPost("CreateVoucher")]
        [Permission(Permissions.JournalVouchers.Create)]
        public async Task<ActionResult<ApiResponse<JournalVoucher>>> CreateManualVoucher([FromBody] JournalVoucherCreateModel model, CancellationToken ct)
        {
            try
            {
                var voucher = await _accountingService.CreateManualVoucherAsync(model, ct);
                return Ok(new ApiResponse<JournalVoucher>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = $"سند حسابداری شماره {voucher.VoucherNumber} با موفقیت ثبت شد",
                    Data = voucher
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

        [HttpGet("TrialBalance")]
        [Permission(Permissions.AccountingReports.View)]
        public async Task<ActionResult<ApiResponse<List<TrialBalanceRowDto>>>> GetTrialBalance(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            CancellationToken ct)
        {
            var data = await _accountingService.GetTrialBalanceAsync(fromDate, toDate, ct);
            return Ok(new ApiResponse<List<TrialBalanceRowDto>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "تراز آزمایشی با موفقیت محاسبه شد",
                Data = data
            });
        }

        [HttpGet("Ledger")]
        [Permission(Permissions.AccountingReports.View)]
        public async Task<ActionResult<ApiResponse<List<LedgerTransactionDto>>>> GetLedger(
            [FromQuery] int? accountId,
            [FromQuery] int? customerId,
            [FromQuery] int? supplierId,
            [FromQuery] int? projectId,
            [FromQuery] int? bankAccountId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            CancellationToken ct)
        {
            var data = await _accountingService.GetLedgerAsync(accountId, customerId, supplierId, projectId, bankAccountId, fromDate, toDate, ct);
            return Ok(new ApiResponse<List<LedgerTransactionDto>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "گردش حساب با موفقیت استخراج شد",
                Data = data
            });
        }

        [HttpGet("ProfitAndLoss")]
        [Permission(Permissions.AccountingReports.View)]
        public async Task<ActionResult<ApiResponse<ProfitAndLossReportDto>>> GetProfitAndLoss(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            CancellationToken ct)
        {
            var data = await _accountingService.GetProfitAndLossAsync(fromDate, toDate, ct);
            return Ok(new ApiResponse<ProfitAndLossReportDto>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "صورت سود و زیان با موفقیت محاسبه شد",
                Data = data
            });
        }

        [HttpPost("CloseFiscalYear")]
        [Permission(Permissions.JournalVouchers.Approve)]
        public async Task<ActionResult<ApiResponse<CloseFiscalYearResultDto>>> CloseFiscalYear(
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
    }
}