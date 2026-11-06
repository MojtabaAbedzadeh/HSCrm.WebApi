using Common.Permissions;
using Framework.Authorization;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Framework.Extensions;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SinaPharm.Common;

namespace HSCrm.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExpenseController : ControllerBase
    {
        private readonly IUnitOfWork _context;
        private readonly IPaymentSettlementService _paymentSettlementService;
        private readonly ICurrentFiscalYearService _currentFiscalYearService;
        private readonly IAccountingService _accountingService;

        public ExpenseController(
            IUnitOfWork context,
            IPaymentSettlementService paymentSettlementService,
            ICurrentFiscalYearService currentFiscalYearService,
            IAccountingService accountingService)
        {
            _context = context;
            _paymentSettlementService = paymentSettlementService;
            _currentFiscalYearService = currentFiscalYearService;
            _accountingService = accountingService;
        }

        [HttpGet("SettlementSummary/{id:int}")]
        [Permission(Permissions.Expenses.View)]
        public IActionResult SettlementSummary(int id)
        {
            var tenantId = User.GetTenantId();
            var fiscalYearId = _currentFiscalYearService.FiscalYearId;

            if (!tenantId.HasValue || !fiscalYearId.HasValue)
                return Unauthorized("دسترسی غیرمجاز یا سال مالی نامعتبر");

            // فیلتر صریح Tenant و FiscalYear برای امنیت داده
            var expense = _context.expenseUW
                .Get(x => x.Id == id && x.TenantId == tenantId.Value && x.FiscalYearId == fiscalYearId.Value)
                .SingleOrDefault();

            if (expense == null)
                return NotFound("هزینه مورد نظر یافت نشد");

            var result = _paymentSettlementService.BuildExpenseSummary(expense.Id, expense.Amount);
            return Ok(result);
        }

        [HttpGet("GetExpense")]
        [Permission(Permissions.Expenses.View)]
        public ApiResponse<IEnumerable<Expense>> Get()
        {
            var tenantId = User.GetTenantId();
            var fiscalYearId = _currentFiscalYearService.FiscalYearId;

            if (!tenantId.HasValue || !fiscalYearId.HasValue)
                return new ApiResponse<IEnumerable<Expense>> { Status = false, Message = "دسترسی نامعتبر" };

            // حذف IgnoreQueryFilters برای حفظ حریم چندمستاجری
            var expenses = _context.expenseUW
                .Get(x => x.TenantId == tenantId.Value && x.FiscalYearId == fiscalYearId.Value,
                     true, x => x.Project, x => x.Tenant, x => x.CreatedBy)
                .ToList();

            return new ApiResponse<IEnumerable<Expense>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "لیست هزینه‌ها با موفقیت دریافت شد",
                Data = expenses
            };
        }

        [HttpGet("GetByProjectId")]
        [Permission(Permissions.Expenses.View)]
        public ApiResponse<IEnumerable<Expense>> GetByProjectId(int projectId)
        {
            var tenantId = User.GetTenantId();
            var fiscalYearId = _currentFiscalYearService.FiscalYearId;

            if (!tenantId.HasValue || !fiscalYearId.HasValue)
                return new ApiResponse<IEnumerable<Expense>> { Status = false, Message = "دسترسی نامعتبر" };

            var expenses = _context.expenseUW
                .Get(
                    x => x.TenantId == tenantId.Value && x.FiscalYearId == fiscalYearId.Value && x.ProjectId == projectId,
                    asNoTracking: true,
                    includes:
                    [
                        x => x.Project,
                        x => x.Tenant,
                        x => x.CreatedBy
                    ]
                )
                .ToList();

            return new ApiResponse<IEnumerable<Expense>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "لیست هزینه‌ها با موفقیت دریافت شد",
                Data = expenses
            };
        }

        [HttpPost("CreateExpense")]
        [Permission(Permissions.Expenses.Create)]
        public async Task<ApiResponse> Create([FromForm] ExpenseCreateModel model)
        {
            if (model == null || !ModelState.IsValid)
                return new ApiResponse { Status = false, Message = "اطلاعات ورودی نامعتبر است" };

            var tenantId = User.GetTenantId();
            var fiscalYearId = _currentFiscalYearService.FiscalYearId;
            var userId = User.GetUserId();

            if (!tenantId.HasValue || !fiscalYearId.HasValue)
                return new ApiResponse { Status = false, Message = "خطا در احراز هویت یا سال مالی" };

            if (model.Amount <= 0 || model.PaidAmount < 0 || model.PaidAmount > model.Amount
                || model.ProjectId <= 0
                || !_context.projectUW.Get(x => x.Id == model.ProjectId && x.TenantId == tenantId.Value && !x.IsDeleted).Any()
                || (model.BankAccountId.HasValue && !_context.bankAccountUW.Get(x => x.Id == model.BankAccountId.Value && x.TenantId == tenantId.Value && !x.IsDeleted).Any()))
                return new ApiResponse { Status = false, Message = "مبلغ، پروژه یا حساب بانکی هزینه معتبر نیست." };

            if (model.PaymentStatus < 0 || model.PaymentStatus > 2)
                return new ApiResponse { Status = false, Message = "وضعیت پرداخت هزینه معتبر نیست." };
            var actualPaidAmount = model.PaymentStatus switch
            {
                1 => model.PaidAmount,
                2 => model.Amount,
                _ => 0m
            };

            DateTime expenseDate;
            try
            {
                expenseDate = ConvertDate.ConvertShamsiToMiladi(model.ExpenseDate);
            }
            catch
            {
                return new ApiResponse { Status = false, Message = "تاریخ وارد شده معتبر نیست" };
            }

            IEntityTransaction? transaction = null;
            try
            {
                // شروع تراکنش داخل try جهت اطمینان از Rollback
                transaction = _context.BeginTransaction();

                var expense = new Expense
                {
                    TenantId = tenantId.Value,
                    FiscalYearId = fiscalYearId.Value,
                    ExpenseTitle = model.ExpenseTitle.Trim(),
                    ProjectId = model.ProjectId,
                    Type = model.Type,
                    ExpenseDate = expenseDate,
                    Amount = model.Amount,
                    Description = model.Description,
                    PaymentStatus = actualPaidAmount <= 0 ? 0 : actualPaidAmount < model.Amount ? 1 : 2,
                    PaidAmount = actualPaidAmount,
                    BankAccountId = model.BankAccountId,
                    PaymentMethod = model.PaymentMethod,
                    ReferenceNo = model.ReferenceNo,
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = userId
                };

                _context.expenseUW.Create(expense);
                await _context.SaveAsync();

                // صدور خودکار سند شناسایی هزینه پروژه
                await _accountingService.CreateVoucherForExpenseAsync(expense);

                // منطق ثبت پرداخت خودکار بر اساس وضعیت


                if (actualPaidAmount > 0)
                {
                    var payment = new Payment
                    {
                        TenantId = tenantId.Value,
                        FiscalYearId = fiscalYearId.Value,
                        ExpenseId = expense.Id,
                        Amount = actualPaidAmount,
                        BankAccountId = model.BankAccountId,
                        Method = model.PaymentMethod ?? "Cash",
                        ReferenceNo = model.ReferenceNo,
                        PaymentDate = expenseDate,
                        CreatedAt = DateTime.UtcNow,
                        CreatedById = userId
                    };
                    _context.paymentUW.Create(payment);
                    await _context.SaveAsync();

                    // صدور خودکار سند پرداخت خزانه بابت هزینه
                    await _accountingService.CreateVoucherForPaymentAsync(payment);
                }

                transaction.Commit();
                return new ApiResponse { Status = true, Message = "هزینه و پرداخت مربوطه با موفقیت ثبت شد" };
            }
            catch (Exception)
            {
                transaction?.Rollback();
                return new ApiResponse { Status = false, Message = "خطا در ثبت عملیات مالی" };
            }
        }

        [HttpPut("UpdateExpense")]
        [Permission(Permissions.Expenses.Edit)]
        public async Task<ApiResponse> Update([FromBody] ExpenseEditModel model)
        {
            if (model == null || model.ExpenseId <= 0)
                return new ApiResponse { Status = false, Message = "شناسه هزینه معتبر نیست" };

            var tenantId = User.GetTenantId();
            var fiscalYearId = _currentFiscalYearService.FiscalYearId;

            if (!tenantId.HasValue || !fiscalYearId.HasValue)
                return new ApiResponse { Status = false, Message = "عدم دسترسی به سال مالی" };

            var expense = _context.expenseUW
                .Get(x => x.Id == model.ExpenseId && x.TenantId == tenantId.Value && x.FiscalYearId == fiscalYearId.Value, false)
                .SingleOrDefault();

            if (expense == null)
                return new ApiResponse { Status = false, Message = "هزینه یافت نشد" };

            if (model.Amount <= 0 || model.ProjectId <= 0
                || !_context.projectUW.Get(x => x.Id == model.ProjectId && x.TenantId == tenantId.Value && !x.IsDeleted).Any()
                || (model.BankAccountId.HasValue && !_context.bankAccountUW.Get(x => x.Id == model.BankAccountId.Value && x.TenantId == tenantId.Value && !x.IsDeleted).Any()))
                return new ApiResponse { Status = false, Message = "مبلغ، پروژه یا حساب بانکی هزینه معتبر نیست." };

            var hasPayments = _context.paymentUW.Get(x =>
                x.ExpenseId == expense.Id && x.TenantId == tenantId.Value
                && x.FiscalYearId == fiscalYearId.Value && !x.IsDeleted).Any();
            if (hasPayments)
                return new ApiResponse
                {
                    Status = false,
                    Message = "این هزینه دارای پرداخت ثبت‌شده است؛ تا زمان فراهم شدن گردش اصلاح و ابطال پرداخت، ویرایش آن مجاز نیست."
                };

            DateTime expenseDate;
            try
            {
                expenseDate = ConvertDate.ConvertShamsiToMiladi(model.ExpenseDate);
            }
            catch
            {
                return new ApiResponse { Status = false, Message = "تاریخ وارد شده معتبر نیست" };
            }

            using var transaction = _context.BeginTransaction();
            try
            {
                // Reverse the old posting and recreate it from the updated expense in one transaction.
                await _accountingService.CancelVoucherByReferenceAsync(
                    HSCrm.Common.PublicTools.Enums.AccountingReferenceType.Expense,
                    expense.Id,
                    "اصلاح اطلاعات هزینه",
                    CancellationToken.None);

                expense.ExpenseTitle = model.ExpenseTitle.Trim();
                expense.Description = model.Description;
                expense.Type = model.Type;
                expense.ProjectId = model.ProjectId;
                expense.Amount = model.Amount;
                expense.ExpenseDate = expenseDate;
                expense.PaidAmount = 0m;
                expense.PaymentStatus = 0;
                expense.BankAccountId = model.BankAccountId;
                expense.PaymentMethod = model.PaymentMethod;
                expense.ReferenceNo = model.ReferenceNo;
                expense.ModifiedAt = DateTime.UtcNow;
                expense.ModifiedById = User.GetUserId();

                _context.expenseUW.Update(expense);
                await _context.SaveAsync();
                await _accountingService.CreateVoucherForExpenseAsync(expense);
                transaction.Commit();

                return new ApiResponse { Status = true, Message = "هزینه و سند حسابداری آن با موفقیت اصلاح شد" };
            }
            catch (Exception)
            {
                transaction.Rollback();
                return new ApiResponse { Status = false, Message = "خطا در بروزرسانی هزینه و سند حسابداری" };
            }
        }

        [HttpDelete("DeleteExpense/{id:int}")]
        [Permission(Permissions.Expenses.Delete)]
        public async Task<ApiResponse> Delete(int id)
        {
            var tenantId = User.GetTenantId();
            var fiscalYearId = _currentFiscalYearService.FiscalYearId;

            if (!tenantId.HasValue || !fiscalYearId.HasValue)
                return new ApiResponse { Status = false, Message = "عدم دسترسی به سال مالی" };

            var expense = _context.expenseUW
                .Get(x => x.Id == id && x.TenantId == tenantId.Value && x.FiscalYearId == fiscalYearId.Value)
                .SingleOrDefault();

            if (expense == null)
                return new ApiResponse { Status = false, Message = "رکورد یافت نشد" };

            var hasPayments = _context.paymentUW.Get(x =>
                x.ExpenseId == expense.Id && x.TenantId == tenantId.Value
                && x.FiscalYearId == fiscalYearId.Value && !x.IsDeleted).Any();
            if (hasPayments)
                return new ApiResponse
                {
                    Status = false,
                    Message = "این هزینه دارای پرداخت ثبت‌شده است؛ ابتدا باید پرداخت‌ها از مسیر حسابداری تسویه و ابطال شوند."
                };

            using var transaction = _context.BeginTransaction();
            try
            {
                await _accountingService.CancelVoucherByReferenceAsync(
                    HSCrm.Common.PublicTools.Enums.AccountingReferenceType.Expense,
                    expense.Id,
                    "حذف هزینه",
                    CancellationToken.None);
                _context.expenseUW.Delete(expense);
                await _context.SaveAsync();
                transaction.Commit();
                return new ApiResponse { Status = true, Message = "هزینه و سند حسابداری آن با موفقیت حذف/ابطال شد" };
            }
            catch (Exception)
            {
                transaction.Rollback();
                return new ApiResponse { Status = false, Message = "خطا در حذف هزینه و سند حسابداری" };
            }
        }
}
}
