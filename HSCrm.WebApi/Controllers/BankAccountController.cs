using Common.Permissions;
using Framework.Authorization;
using HSCrm.Common.PublicTools;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Framework.Extensions;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCrm.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class BankAccountController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAccountingService _accountingService;

        public BankAccountController(IUnitOfWork unitOfWork, IAccountingService accountingService)
        {
            _unitOfWork = unitOfWork;
            _accountingService = accountingService;
        }

        /// <summary>
        /// دریافت لیست حساب‌های بانکی و صندوق‌های شرکت
        /// </summary>
        [HttpGet("GetAll")]
        [Permission(Permissions.BankAccounts.View)]
        public async Task<ActionResult<ApiResponse<List<BankAccount>>>> GetAll(CancellationToken ct)
        {
            var list = await _unitOfWork.bankAccountUW
                .Get(x => !x.IsDeleted, asNoTracking: true)
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.Id)
                .ToListAsync(ct);

            return Ok(new ApiResponse<List<BankAccount>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "لیست حساب‌های بانکی با موفقیت دریافت شد.",
                Data = list
            });
        }

        /// <summary>
        /// گزارش خلاصه موجودی لحظه‌ای، کل واریزی‌ها و کل برداشت‌های هر حساب بانکی / صندوق
        /// </summary>
        [HttpGet("BalancesSummary")]
        [Permission(Permissions.BankAccounts.View)]
        public async Task<ActionResult<ApiResponse<List<BankAccountBalanceSummaryDto>>>> GetBalancesSummary(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            CancellationToken ct)
        {
            var data = await _accountingService.GetBankAccountsBalanceSummaryAsync(fromDate, toDate, ct);
            return Ok(new ApiResponse<List<BankAccountBalanceSummaryDto>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "موجودی و خلاصه گردش حساب‌های بانکی محاسبه شد.",
                Data = data
            });
        }

        /// <summary>
        /// دریافت ریز گردش (صورت‌حساب) یک حساب بانکی مشخص (تمام پرداخت‌های هزینه، خرید تجهیزات، دریافت‌های فروش و ...)
        /// </summary>
        [HttpGet("Statement/{bankAccountId:int}")]
        [Permission(Permissions.BankAccounts.View)]
        public async Task<ActionResult<ApiResponse<List<LedgerTransactionDto>>>> GetBankStatement(
            int bankAccountId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            CancellationToken ct)
        {
            var data = await _accountingService.GetLedgerAsync(
                bankAccountId: bankAccountId,
                fromDate: fromDate,
                toDate: toDate,
                ct: ct);

            return Ok(new ApiResponse<List<LedgerTransactionDto>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "صورت‌حساب و ریز تراکنش‌های حساب بانکی استخراج شد.",
                Data = data
            });
        }

        /// <summary>
        /// تعریف حساب بانکی / صندوق جدید + صدور خودکار سند موجودی اول دوره
        /// </summary>
        [HttpPost("Create")]
        [Permission(Permissions.BankAccounts.Create)]
        public async Task<ActionResult<ApiResponse<BankAccount>>> Create(
            [FromBody] BankAccountCreateModel model,
            CancellationToken ct)
        {
            var tenantId = User.GetTenantId();
            if (!tenantId.HasValue)
                return Unauthorized(new ApiResponse { Status = false, Message = "دسترسی نامعتبر" });

            using var transaction = await _unitOfWork.BeginTransactionAsync(ct);
            try
            {
                if (model.IsDefault)
                {
                    var existingDefaults = await _unitOfWork.bankAccountUW
                        .Get(x => x.TenantId == tenantId.Value && x.IsDefault && !x.IsDeleted, asNoTracking: false)
                        .ToListAsync(ct);

                    foreach (var item in existingDefaults)
                    {
                        item.IsDefault = false;
                        _unitOfWork.bankAccountUW.Update(item);
                    }
                }

                var bankAccount = new BankAccount
                {
                    TenantId = tenantId.Value,
                    Title = model.Title.Trim(),
                    BankName = model.BankName.Trim(),
                    AccountNumber = model.AccountNumber?.Trim(),
                    ShebaNumber = model.ShebaNumber?.Trim(),
                    CardNumber = model.CardNumber?.Trim(),
                    InitialBalance = model.InitialBalance,
                    IsDefault = model.IsDefault,
                    IsActive = true,
                    Description = model.Description?.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = User.GetUserId()
                };

                await _unitOfWork.bankAccountUW.CreateAsync(bankAccount, ct);
                await _unitOfWork.SaveAsync();

                if (bankAccount.InitialBalance > 0)
                {
                    await _accountingService.CreateOpeningVoucherForBankAccountAsync(bankAccount, ct);
                }

                await transaction.CommitAsync(ct);

                return Ok(new ApiResponse<BankAccount>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "حساب بانکی با موفقیت ایجاد شد.",
                    Data = bankAccount
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                return BadRequest(new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "خطا در ثبت حساب بانکی: " + ex.Message
                });
            }
        }

        [HttpPut("Update")]
        [Permission(Permissions.BankAccounts.Edit)]
        public async Task<ActionResult<ApiResponse<BankAccount>>> Update(
            [FromBody] BankAccountEditModel model,
            CancellationToken ct)
        {
            var tenantId = User.GetTenantId();
            if (!tenantId.HasValue)
                return Unauthorized(new ApiResponse { Status = false, Message = "دسترسی نامعتبر" });

            var bankAccount = await _unitOfWork.bankAccountUW.GetByIdAsync(model.Id, ct);
            if (bankAccount == null || bankAccount.TenantId != tenantId.Value || bankAccount.IsDeleted)
                return NotFound(new ApiResponse { Status = false, Message = "حساب بانکی یافت نشد." });

            if (model.IsDefault && !bankAccount.IsDefault)
            {
                var existingDefaults = await _unitOfWork.bankAccountUW
                    .Get(x => x.TenantId == tenantId.Value && x.IsDefault && x.Id != bankAccount.Id && !x.IsDeleted, asNoTracking: false)
                    .ToListAsync(ct);

                foreach (var item in existingDefaults)
                {
                    item.IsDefault = false;
                    _unitOfWork.bankAccountUW.Update(item);
                }
            }

            bankAccount.Title = model.Title.Trim();
            bankAccount.BankName = model.BankName.Trim();
            bankAccount.AccountNumber = model.AccountNumber?.Trim();
            bankAccount.ShebaNumber = model.ShebaNumber?.Trim();
            bankAccount.CardNumber = model.CardNumber?.Trim();
            bankAccount.IsDefault = model.IsDefault;
            bankAccount.IsActive = model.IsActive;
            bankAccount.Description = model.Description?.Trim();
            bankAccount.ModifiedAt = DateTime.UtcNow;
            bankAccount.ModifiedById = User.GetUserId();

            _unitOfWork.bankAccountUW.Update(bankAccount);
            await _unitOfWork.SaveAsync();

            return Ok(new ApiResponse<BankAccount>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "اطلاعات حساب بانکی با موفقیت ویرایش شد.",
                Data = bankAccount
            });
        }

        [HttpDelete("Delete/{id:int}")]
        [Permission(Permissions.BankAccounts.Delete)]
        public async Task<ActionResult<ApiResponse>> Delete(int id, CancellationToken ct)
        {
            var tenantId = User.GetTenantId();
            if (!tenantId.HasValue)
                return Unauthorized(new ApiResponse { Status = false, Message = "دسترسی نامعتبر" });

            var bankAccount = await _unitOfWork.bankAccountUW.GetByIdAsync(id, ct);
            if (bankAccount == null || bankAccount.TenantId != tenantId.Value || bankAccount.IsDeleted)
                return NotFound(new ApiResponse { Status = false, Message = "حساب بانکی یافت نشد." });

            bankAccount.IsDeleted = true;
            bankAccount.DeletedAt = DateTime.UtcNow;
            bankAccount.DeletedById = User.GetUserId();

            _unitOfWork.bankAccountUW.Update(bankAccount);
            await _unitOfWork.SaveAsync();

            return Ok(new ApiResponse
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "حساب بانکی با موفقیت حذف شد."
            });
        }
    }
}
