using Common.Permissions;
using Framework.Authorization;
using HSCrm.Common.PublicTools;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Entities.Models.Dto;
using HSCrm.Framework.Extensions;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static HSCrm.Common.PublicTools.Enums;
using static HSCrm.Common.PublicTools.StringCleaner;

namespace HSCrm.WebApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IUnitOfWork _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly ICurrentFiscalYearService _currentFiscalYearService;
        private readonly IAccountingService _accountingService;

        public PaymentController(
            IUnitOfWork context,
            ICurrentUserService currentUserService,
            ICurrentFiscalYearService currentFiscalYearService,
            IAccountingService accountingService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _currentFiscalYearService = currentFiscalYearService;
            _accountingService = accountingService;
        }

        //[HttpGet("GetPayments")]
        //[Permission(Permissions.Payments.View)]
        //public async Task<ApiResponse<IEnumerable<Payment>>> Get()
        //{
        //    var tenantId = _currentUserService.TenantId;

        //    if (tenantId is null)
        //    {
        //        return new ApiResponse<IEnumerable<Payment>>
        //        {
        //            Status = false,
        //            StatusCode = ApiResultStatusCode.Unauthorized,
        //            Message = "Tenant is missing."
        //        };
        //    }

        //    var payments =  _context.paymentUW
        //        .Get(p => p.TenantId == tenantId.Value)
        //        .ToList();

        //    return new ApiResponse<IEnumerable<Payment>>
        //    {
        //        Status = true,
        //        StatusCode = ApiResultStatusCode.Success,
        //        Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
        //        Data = payments
        //    };
        //}

        [HttpGet("GetPayments")]
        [Permission(Permissions.Payments.View)]
        public async Task<ApiResponse<IEnumerable<PaymentDto>>> Get(CancellationToken cancellationToken)
        {
            var tenantId = _currentUserService.TenantId;

            if (tenantId is null)
            {
                return new ApiResponse<IEnumerable<PaymentDto>>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Unauthorized,
                    Message = "Tenant is missing."
                };
            }

            var payments = await _context.paymentUW.GetAsync(
                predicate: p => p.TenantId == tenantId.Value,
                asNoTracking: true,
                ct: cancellationToken,

                // Navigationهای موردنیاز
                p => p.BankAccount!,
                p => p.PurchaseInvoice!,
                p => p.Expense!,
                p => p.Expense!.Project!
            );

            var result = payments.Select(p => new PaymentDto
            {
                Id = p.Id,
                Amount = p.Amount,
                PaymentDate = p.PaymentDate,
                Method = p.Method,
                ReferenceNo = p.ReferenceNo,
                BankAccountId = p.BankAccountId,
                BankAccountTitle = p.BankAccount != null ? $"{p.BankAccount.Title} ({p.BankAccount.BankName})" : null,

                PurchaseInvoiceId = p.PurchaseInvoiceId,
                ExpenseId = p.ExpenseId,

                Title = p.PurchaseInvoiceId.HasValue
                    ? $"پرداخت از بابت فاکتور شماره  {p.PurchaseInvoice?.Number ?? "-"}"
                    : p.ExpenseId.HasValue
                    ? $"پرداخت از بابت {p.Expense?.ExpenseTitle ?? "-"}" +
                          $"{(p.Expense?.Project != null ? $" پروژه {p.Expense.Project.ProjectTitle}" : "")}"
                        : "پرداخت بدون مرجع"
            })
            .OrderByDescending(x => x.PaymentDate)
            .ToList();

            return new ApiResponse<IEnumerable<PaymentDto>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = result
            };
        }

        [HttpGet("ReferenceAutocomplete")]
        [Permission(Permissions.Payments.View)]
        public ApiResponse<IEnumerable<PaymentReferenceDto>> ReferenceAutocomplete(string term, string referenceType = "all")
        {
            term ??= string.Empty;
            referenceType = string.IsNullOrWhiteSpace(referenceType)
                ? "all"
                : referenceType.Trim();

            if (term.Length < 1)
            {
                return new ApiResponse<IEnumerable<PaymentReferenceDto>>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "موفق",
                    Data = Enumerable.Empty<PaymentReferenceDto>()
                };
            }

            var tenantId = _currentUserService.TenantId ?? User.GetTenantId();
            var fiscalYearId = _currentFiscalYearService.FiscalYearId;

            var data = new List<PaymentReferenceDto>();

            if (referenceType is "all" or "purchaseInvoice")
            {
                var purchaseInvoices = _context.purchaseInvoiceUW
                    .Get(x => (!tenantId.HasValue || x.TenantId == tenantId.Value)
                              && x.FiscalYearId == fiscalYearId
                              && (x.Number.Contains(term) || (x.Supplier != null && x.Supplier.SupplierFullName.Contains(term))),
                    true,
                    x => x.Supplier)
                        .Take(15)
                        .ToList();


                data.AddRange(purchaseInvoices.Select(x => new PaymentReferenceDto
                {
                    Id = x.Id,
                    Type = "purchaseInvoice",
                    Title = $"{x.Number} - {x.Supplier?.SupplierFullName ?? "-"}",
                    Amount = x.Total,
                    Date = x.IssueDate,
                    SupplierId = x.SupplierId
                }));
            }

            if (referenceType is "all" or "expense")
            {
                var expenses = _context.expenseUW
                    .Get(
                        x =>
                            (!tenantId.HasValue || x.TenantId == tenantId.Value) &&
                            x.FiscalYearId == fiscalYearId &&
                            x.ExpenseTitle.Contains(term),
                        true,
                        x => x.Project)
                    .Take(15)
                    .ToList();


                data.AddRange(expenses.Select(x => new PaymentReferenceDto
                {
                    Id = x.Id,
                    Type = "expense",
                    Title = $"{x.ExpenseTitle} - {x.Project?.ProjectTitle ?? "-"}",
                    Amount = x.Amount,
                    Date = x.ExpenseDate,
                    ProjectId = x.ProjectId
                }));
            }

            return new ApiResponse<IEnumerable<PaymentReferenceDto>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "موفق",
                Data = data
                    .OrderByDescending(x => x.Date)
                    .Take(20)
                    .ToList()
            };
        }

        [HttpPost("Create")]
        [Permission(Permissions.Payments.Create)]
        public async Task<ApiResponse> Create([FromBody] PaymentModel model)
        {
            // 1. اعتبارسنجی ورودی: فقط یکی از فاکتور خرید یا هزینه باید مقدار داشته باشد
            if (model.PurchaseInvoiceId.HasValue == model.ExpenseId.HasValue)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "لطفاً دقیقاً یکی از موارد فاکتور خرید یا هزینه را انتخاب کنید."
                };
            }

            var tenantId = _currentUserService.TenantId ?? User.GetTenantId();
            var fiscalYearId = model.FiscalYearId > 0 ? model.FiscalYearId : (_currentFiscalYearService.FiscalYearId ?? 0);
            if (!tenantId.HasValue || fiscalYearId <= 0)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Unauthorized,
                    Message = "اطلاعات شرکت یا سال مالی معتبر نیست."
                };
            }

            var tenant = _context.tenantUW.GetById(tenantId.Value);
            if (tenant?.DocumentLockDate != null && model.PaymentDate.Date <= tenant.DocumentLockDate.Value.Date)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "ثبت پرداخت در تاریخ‌های قفل‌شده امکان‌پذیر نیست."
                };
            }

            var userId = User.GetUserId();

            // شروع تراکنش برای اطمینان از ثبت پرداخت و آپدیت فاکتور با هم
            using var transaction = _context.BeginTransaction();
            try
            {
                // 2. ثبت رکورد پرداخت جدید
                var entity = new Payment
                {
                    TenantId = tenantId.Value,
                    FiscalYearId = fiscalYearId,
                    PurchaseInvoiceId = model.PurchaseInvoiceId,
                    ExpenseId = model.ExpenseId,
                    PaymentDate = model.PaymentDate,
                    Amount = model.Amount,
                    BankAccountId = model.BankAccountId,
                    Method = model.Method,
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = userId,
                    ReferenceNo = model.ReferenceNo,
                    Description = model.Description
                };

                _context.paymentUW.Create(entity);
                await _context.SaveAsync();

                // صدور خودکار سند پرداخت خزانه
                await _accountingService.CreateVoucherForPaymentAsync(entity);

                // 3. به‌روزرسانی موجودیت مرتبط (فاکتور خرید یا هزینه)
                if (model.PurchaseInvoiceId.HasValue)
                {
                    UpdatePurchaseInvoiceStatus(model.PurchaseInvoiceId.Value);
                }
                else if (model.ExpenseId.HasValue)
                {
                    UpdateExpenseStatus(model.ExpenseId.Value);
                }

                // تایید نهایی تراکنش
                transaction.Commit();

                return new ApiResponse
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "پرداخت با موفقیت ثبت و وضعیت فاکتور به‌روزرسانی شد."
                };
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.ServerError,
                    Message = "خطا در ثبت پرداخت: " + ex.Message
                };
            }
        }

        // متد کمکی برای محاسبه مجدد و آپدیت فاکتور خرید
        private void UpdatePurchaseInvoiceStatus(int invoiceId)
        {
            var invoice = _context.purchaseInvoiceUW.GetById(invoiceId);
            if (invoice == null) return;

            // محاسبه مجموع تمامی پرداخت‌های غیرحذفی + چک‌های پرداختنی معتبر برای این فاکتور
            var cashPaid = _context.paymentUW.Get(p => p.PurchaseInvoiceId == invoiceId && !p.IsDeleted)
                                             .Sum(p => p.Amount);
            var chequePaid = _context.chequeUW.Get(c => c.PurchaseInvoiceId == invoiceId
                                                     && c.Type == ChequeType.Payable
                                                     && !c.IsDeleted
                                                     && c.Status != ChequeStatus.Bounced
                                                     && c.Status != ChequeStatus.Returned)
                                             .Sum(c => c.Amount);
            var totalPaid = cashPaid + chequePaid;

            invoice.PaidAmount = totalPaid;
            // محاسبه وضعیت تسویه
            if (invoice.PaidAmount <= 0)
                invoice.PayStatus = SettlementStatus.Unpaid;
            else if (invoice.PaidAmount < invoice.Total)
                invoice.PayStatus = SettlementStatus.Partial;
            else
                invoice.PayStatus = SettlementStatus.Paid;

            _context.purchaseInvoiceUW.Update(invoice);
            _context.Save();
        }

        // متد کمکی برای محاسبه مجدد و آپدیت هزینه
        private void UpdateExpenseStatus(int expenseId)
        {
            var expense = _context.expenseUW.GetById(expenseId);
            if (expense == null) return;

            // محاسبه مجموع تمامی پرداخت‌های غیرحذفی + چک‌های پرداختنی معتبر برای این هزینه
            var cashPaid = _context.paymentUW.Get(p => p.ExpenseId == expenseId && !p.IsDeleted)
                                             .Sum(p => p.Amount);
            var chequePaid = _context.chequeUW.Get(c => c.ExpenseId == expenseId
                                                     && c.Type == ChequeType.Payable
                                                     && !c.IsDeleted
                                                     && c.Status != ChequeStatus.Bounced
                                                     && c.Status != ChequeStatus.Returned)
                                             .Sum(c => c.Amount);
            var totalPaid = cashPaid + chequePaid;

            expense.PaidAmount = totalPaid;
            // فرض بر این است که برای هزینه هم فیلد Amount مبلغ کل است
            if (expense.PaidAmount <= 0)
                expense.PaymentStatus = (int)SettlementStatus.Unpaid;
            else if (expense.PaidAmount < expense.Amount)
                expense.PaymentStatus = (int)SettlementStatus.Partial;
            else
                expense.PaymentStatus = (int)SettlementStatus.Paid;

            _context.expenseUW.Update(expense);
            _context.Save();
        }

        [HttpPost("RegisterPurchasePayment")]
        public async Task<ApiResponse> RegisterPurchasePayment([FromBody] RegisterPurchasePaymentModel model)
        {
            try
            {
                if (model == null) return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
                };

                if (model.PurchaseInvoiceId <= 0) return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
                };

                if (model.Amount <= 0) return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
                };

                var tenantId = _currentUserService.TenantId ?? User.GetTenantId();
                var invoice = _context.purchaseInvoiceUW.GetById(model.PurchaseInvoiceId);
                if (invoice == null || (tenantId.HasValue && invoice.TenantId != tenantId.Value)) return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
                };

                var tenant = _context.tenantUW.GetById(invoice.TenantId);
                if (tenant?.DocumentLockDate != null && model.PaymentDate.Date <= tenant.DocumentLockDate.Value.Date)
                {
                    return new ApiResponse
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.BadRequest,
                        Message = "ثبت پرداخت در تاریخ‌های قفل‌شده امکان‌پذیر نیست."
                    };
                }

                if (invoice.Status != InvoiceStatus.Final) return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
                };

                var alreadyPaid = _context.paymentUW
                    .Get(x => x.PurchaseInvoiceId == invoice.Id && !x.IsDeleted)
                    .Sum(x => x.Amount)
                    + _context.chequeUW
                    .Get(c => c.PurchaseInvoiceId == invoice.Id
                           && c.Type == ChequeType.Payable
                           && !c.IsDeleted
                           && c.Status != ChequeStatus.Bounced
                           && c.Status != ChequeStatus.Returned)
                    .Sum(c => c.Amount);

                var remaining = invoice.Total - alreadyPaid;
                if (model.Amount > remaining) return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
                };

                var payment = new Payment
                {
                    TenantId = invoice.TenantId,
                    FiscalYearId = invoice.FiscalYearId,
                    PurchaseInvoiceId = invoice.Id,
                    Amount = model.Amount,
                    BankAccountId = model.BankAccountId,
                    PaymentDate = model.PaymentDate,
                    Method = model.PaymentMethod,
                    Description = model.Description,
                    CreatedById = User.GetUserId(),
                    CreatedAt = DateTime.UtcNow
                };

                _context.paymentUW.Create(payment);
                await _context.SaveAsync();

                await _accountingService.CreateVoucherForPaymentAsync(payment);

                var totalPaid = _context.paymentUW
                    .Get(x => x.PurchaseInvoiceId == invoice.Id && !x.IsDeleted)
                    .Sum(x => x.Amount)
                    + _context.chequeUW
                    .Get(c => c.PurchaseInvoiceId == invoice.Id
                           && c.Type == ChequeType.Payable
                           && !c.IsDeleted
                           && c.Status != ChequeStatus.Bounced
                           && c.Status != ChequeStatus.Returned)
                    .Sum(c => c.Amount);

                invoice.PaidAmount = totalPaid;
                invoice.PayStatus = totalPaid <= 0
                    ? SettlementStatus.Unpaid
                    : totalPaid < invoice.Total
                        ? SettlementStatus.Partial
                        : SettlementStatus.Paid;

                _context.purchaseInvoiceUW.Update(invoice);
                await _context.SaveAsync();

                return new ApiResponse
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                    AdditionalData = invoice
                };
            }
            catch (Exception)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.ServerError,
                    Message = ApiResultStatusCode.ServerError.DisplayNameAttribute(DisplayProperty.Name)
                };
            }
        }
    }
}