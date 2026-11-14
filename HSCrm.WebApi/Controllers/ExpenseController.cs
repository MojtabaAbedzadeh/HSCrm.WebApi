using Microsoft.AspNetCore.Authorization;
﻿using Common.Permissions;
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
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ExpenseController : ControllerBase
    {
        private readonly IUnitOfWork _context;
        private readonly IPaymentSettlementService _paymentSettlementService;
        private readonly ICurrentFiscalYearService _currentFiscalYearService;
        private readonly IAccountingService _accountingService;
        private readonly IProjectAccessService? _projectAccess;

        public ExpenseController(
            IUnitOfWork context,
            IPaymentSettlementService paymentSettlementService,
            ICurrentFiscalYearService currentFiscalYearService,
            IAccountingService accountingService,
            IProjectAccessService? projectAccess = null)
        {
            _context = context;
            _paymentSettlementService = paymentSettlementService;
            _currentFiscalYearService = currentFiscalYearService;
            _accountingService = accountingService;
            _projectAccess = projectAccess;
        }

        [HttpGet("SettlementSummary/{id:int}")]
        [Permission(Permissions.Expenses.View)]
        public async Task<IActionResult> SettlementSummary(int id, CancellationToken ct = default)
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
            var userId = User.GetUserId();
            if (_projectAccess != null && !string.IsNullOrWhiteSpace(userId) && !await _projectAccess.CanAccessProjectAsync(tenantId.Value, userId, expense.ProjectId, ct))
                return StatusCode(403, new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = "شما به این پروژه دسترسی ندارید." });

            var result = _paymentSettlementService.BuildExpenseSummary(expense.Id, expense.Amount);
            return Ok(result);
        }

        [HttpGet("GetExpense")]
        [Permission(Permissions.Expenses.View)]
        public async Task<ApiResponse<IEnumerable<Expense>>> Get(CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var fiscalYearId = _currentFiscalYearService.FiscalYearId;

            if (!tenantId.HasValue || !fiscalYearId.HasValue)
                return new ApiResponse<IEnumerable<Expense>> { Status = false, Message = "دسترسی نامعتبر" };

            // حذف IgnoreQueryFilters برای حفظ حریم چندمستاجری
            var query = _context.expenseUW
                .Get(x => x.TenantId == tenantId.Value && x.FiscalYearId == fiscalYearId.Value,
                     true, x => x.Project, x => x.Tenant, x => x.CreatedBy);

            var userId = User.GetUserId();
            if (_projectAccess != null && !string.IsNullOrWhiteSpace(userId) && !await _projectAccess.HasFullProjectAccessAsync(userId, ct))
            {
                var allowedIds = await _projectAccess.GetAccessibleProjectIdsAsync(tenantId.Value, userId, ct);
                query = query.Where(x => allowedIds.Contains(x.ProjectId));
            }

            var expenses = query
                .OrderByDescending(x => x.ExpenseDate)
                .ThenByDescending(x => x.Id)
                .ToList();

            return new ApiResponse<IEnumerable<Expense>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "لیست هزینه‌ها با موفقیت دریافت شد",
                Data = expenses
            };
        }

        [HttpGet("GetById")]
        [Permission(Permissions.Expenses.View)]
        public async Task<ApiResponse<Expense>> GetById(int expenseId, CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var fiscalYearId = _currentFiscalYearService.FiscalYearId;

            if (!tenantId.HasValue || !fiscalYearId.HasValue)
                return new ApiResponse<Expense> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" };

            var expense = _context.expenseUW
                .Get(x => x.Id == expenseId && x.TenantId == tenantId.Value && x.FiscalYearId == fiscalYearId.Value,
                     true, x => x.Project, x => x.Tenant, x => x.CreatedBy)
                .SingleOrDefault();

            if (expense == null)
                return new ApiResponse<Expense> { Status = false, StatusCode = ApiResultStatusCode.NotFound, Message = "هزینه یافت نشد" };

            var userId = User.GetUserId();
            if (_projectAccess != null && !string.IsNullOrWhiteSpace(userId) && !await _projectAccess.CanAccessProjectAsync(tenantId.Value, userId, expense.ProjectId, ct))
            {
                if (HttpContext != null) HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                return new ApiResponse<Expense> { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = "شما به این پروژه دسترسی ندارید." };
            }

            return new ApiResponse<Expense>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "دریافت اطلاعات هزینه موفق بود",
                Data = expense
            };
        }

        [HttpGet("GetByProjectId")]
        [HttpGet("GetProjectExpenses")]
        [Permission(Permissions.Expenses.View)]
        public async Task<ApiResponse<IEnumerable<Expense>>> GetByProjectId(int projectId, CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var fiscalYearId = _currentFiscalYearService.FiscalYearId;

            if (!tenantId.HasValue || !fiscalYearId.HasValue)
                return new ApiResponse<IEnumerable<Expense>> { Status = false, Message = "دسترسی نامعتبر" };

            var userId = User.GetUserId();
            if (_projectAccess != null && !string.IsNullOrWhiteSpace(userId) && !await _projectAccess.CanAccessProjectAsync(tenantId.Value, userId, projectId, ct))
            {
                if (HttpContext != null) HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                return new ApiResponse<IEnumerable<Expense>> { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = "شما عضو این پروژه نیستید و مجاز به مشاهده هزینه‌های آن نمی‌باشید." };
            }

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

            if (_projectAccess != null && !string.IsNullOrWhiteSpace(userId) && !await _projectAccess.CanAccessProjectAsync(tenantId.Value, userId, model.ProjectId))
            {
                if (HttpContext != null) HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                return new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = "شما عضو این پروژه نیستید و مجاز به ثبت هزینه برای آن نمی‌باشید." };
            }

            DateTime expenseDate;
            try
            {
                expenseDate = ConvertDate.ConvertShamsiToMiladi(model.ExpenseDate);
            }
            catch
            {
                return new ApiResponse { Status = false, Message = "تاریخ وارد شده معتبر نیست" };
            }

            var tenant = _context.tenantUW.GetById(tenantId.Value);
            if (tenant?.DocumentLockDate != null && expenseDate.Date <= tenant.DocumentLockDate.Value.Date)
                return new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = "ثبت سند در تاریخ‌های قفل‌شده امکان‌پذیر نیست." };

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
        public async Task<ApiResponse> Update([FromBody] ExpenseEditModel model, CancellationToken ct = default)
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

            var actorId = User.GetUserId();
            if (_projectAccess != null && !string.IsNullOrWhiteSpace(actorId))
            {
                if (!await _projectAccess.CanAccessProjectAsync(tenantId.Value, actorId, expense.ProjectId, ct) ||
                    !await _projectAccess.CanAccessProjectAsync(tenantId.Value, actorId, model.ProjectId, ct))
                {
                    if (HttpContext != null) HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = "شما به این پروژه دسترسی ندارید." };
                }
            }

            DateTime parsedDate;
            try
            {
                parsedDate = ConvertDate.ConvertShamsiToMiladi(model.ExpenseDate);
            }
            catch
            {
                return new ApiResponse { Status = false, Message = "تاریخ وارد شده معتبر نیست" };
            }

            var tenant = _context.tenantUW.GetById(tenantId.Value);
            if (tenant?.DocumentLockDate != null && (parsedDate.Date <= tenant.DocumentLockDate.Value.Date || expense.ExpenseDate.Date <= tenant.DocumentLockDate.Value.Date))
                return new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = "ویرایش سند در تاریخ‌های قفل‌شده امکان‌پذیر نیست." };

            try
            {
                expense.ExpenseTitle = model.ExpenseTitle.Trim();
                expense.Description = model.Description;
                expense.Type = model.Type;
                expense.ProjectId = model.ProjectId;
                expense.Amount = model.Amount;
                expense.ExpenseDate = parsedDate;
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
        public async Task<ApiResponse> Delete(int id, CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var fiscalYearId = _currentFiscalYearService.FiscalYearId;

            if (!tenantId.HasValue) return new ApiResponse { Status = false, Message = "عدم دسترسی" };

            var expense = _context.expenseUW
                .Get(x => x.Id == id && x.TenantId == tenantId.Value && x.FiscalYearId == fiscalYearId.Value)
                .SingleOrDefault();

            if (expense == null)
                return new ApiResponse { Status = false, Message = "رکورد یافت نشد" };

            var actorId = User.GetUserId();
            if (_projectAccess != null && !string.IsNullOrWhiteSpace(actorId) && !await _projectAccess.CanAccessProjectAsync(tenantId.Value, actorId, expense.ProjectId, ct))
            {
                if (HttpContext != null) HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                return new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = "شما به این پروژه دسترسی ندارید." };
            }

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