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
                    PaymentStatus = model.PaymentStatus,
                    PaidAmount = model.PaidAmount,
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
                var actualPaidAmount = model.PaymentStatus switch
                {
                    1 => model.PaidAmount,      // پرداخت جزئی
                    2 => model.Amount,          // پرداخت کامل
                    _ => 0m                     // عدم پرداخت
                };

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
        public ApiResponse Update([FromBody] ExpenseEditModel model)
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

            try
            {
                expense.ExpenseTitle = model.ExpenseTitle.Trim();
                expense.Description = model.Description;
                expense.Type = model.Type;
                expense.ProjectId = model.ProjectId;
                expense.Amount = model.Amount;
                expense.ExpenseDate = ConvertDate.ConvertShamsiToMiladi(model.ExpenseDate);
                expense.PaymentStatus = model.PaymentStatus;
                expense.PaidAmount = model.PaidAmount;
                expense.BankAccountId = model.BankAccountId;
                expense.PaymentMethod = model.PaymentMethod;
                expense.ReferenceNo = model.ReferenceNo;
                expense.ModifiedAt = DateTime.UtcNow;
                expense.ModifiedById = User.GetUserId();

                _context.expenseUW.Update(expense);
                _context.Save();

                return new ApiResponse { Status = true, Message = "هزینه با موفقیت ویرایش شد" };
            }
            catch (Exception)
            {
                return new ApiResponse { Status = false, Message = "خطا در بروزرسانی هزینه" };
            }
        }

        [HttpDelete("DeleteExpense/{id:int}")]
        [Permission(Permissions.Expenses.Delete)]
        public ApiResponse Delete(int id)
        {
            var tenantId = User.GetTenantId();
            var fiscalYearId = _currentFiscalYearService.FiscalYearId;

            if (!tenantId.HasValue) return new ApiResponse { Status = false, Message = "عدم دسترسی" };

            var expense = _context.expenseUW
                .Get(x => x.Id == id && x.TenantId == tenantId.Value && x.FiscalYearId == fiscalYearId.Value)
                .SingleOrDefault();

            if (expense == null)
                return new ApiResponse { Status = false, Message = "رکورد یافت نشد" };

            try
            {
                _context.expenseUW.Delete(expense);
                _context.Save();
                return new ApiResponse { Status = true, Message = "هزینه حذف شد" };
            }
            catch (Exception)
            {
                return new ApiResponse { Status = false, Message = "خطا در حذف هزینه" };
            }
        }
    }
}