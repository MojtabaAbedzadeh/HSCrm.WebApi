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
using SinaPharm.Common;
using System.Globalization;
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

        public PaymentController(IUnitOfWork context, ICurrentUserService currentUserService, ICurrentFiscalYearService currentFiscalYearService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _currentFiscalYearService = currentFiscalYearService;
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

                PurchaseInvoiceId = p.PurchaseInvoiceId,
                ExpenseId = p.ExpenseId,

                Title = p.PurchaseInvoiceId.HasValue
                    ? $"پرداخت از بابت فاکتور شماره  {p.PurchaseInvoice.Number}"
                    : p.ExpenseId.HasValue
                    ? $"پرداخت از بابت {p.Expense?.ExpenseTitle}" +
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

            if (term.Length < 2)
            {
                return new ApiResponse<IEnumerable<PaymentReferenceDto>>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "موفق",
                    Data = Enumerable.Empty<PaymentReferenceDto>()
                };
            }

            var fiscalYearId = _currentFiscalYearService.FiscalYearId;

            var data = new List<PaymentReferenceDto>();

            if (referenceType is "all" or "purchaseInvoice")
            {
                var purchaseInvoices = _context.purchaseInvoiceUW
                    .Get(x => x.FiscalYearId == fiscalYearId && (x.Number.Contains(term) || x.Supplier.SupplierFullName.Contains(term)),
                    true,
                    x => x.Supplier)
                        .Take(15)
                        .ToList();


                data.AddRange(purchaseInvoices.Select(x => new PaymentReferenceDto
                {
                    Id = x.Id,
                    Type = "purchaseInvoice",
                    Title = $"{x.Number} - {x.Supplier.SupplierFullName}",
                    Amount = x.Total,
                    Date = x.IssueDate
                }));
            }

            if (referenceType is "all" or "expense")
            {
                var expenses = _context.expenseUW
                    .Get(
                        x =>
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
                    Title = $"{x.ExpenseTitle} - {x.Project.ProjectTitle}",
                    Amount = x.Amount,
                    Date = x.ExpenseDate
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
        public ApiResponse Create([FromBody] PaymentModel model)
        {
            if (model.PurchaseInvoiceId.HasValue == model.ExpenseId.HasValue)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "فقط یکی از فاکتور خرید یا هزینه باید انتخاب شود."
                };
            }
            var userId = User.GetUserId();

            var entity = new Payment
            {
                FiscalYearId = model.FiscalYearId,
                PurchaseInvoiceId = model.PurchaseInvoiceId,
                ExpenseId = model.ExpenseId,
                PaymentDate = model.PaymentDate,
                Amount = model.Amount,
                Method = model.Method,
                CreatedAt = DateTime.Now,
                CreatedById = userId,
                ReferenceNo = model.ReferenceNo
            };

            _context.paymentUW.Create(entity);
            _context.Save();

            return new ApiResponse
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "پرداخت با موفقیت ثبت شد."
            };
        }

        [HttpPost("RegisterPurchasePayment")]
        public ApiResponse RegisterPurchasePayment([FromBody] RegisterPurchasePaymentModel model)
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

                var invoice = _context.purchaseInvoiceUW.GetById(model.PurchaseInvoiceId);
                if (invoice == null) return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
                };

                if (invoice.Status != SalesInvoiceStatus.Final) return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
                };

                var alreadyPaid = _context.paymentUW
                    .Get(x => x.PurchaseInvoiceId == invoice.Id && !x.IsDeleted)
                    .Sum(x => x.Amount);

                var remaining = invoice.Total - alreadyPaid;
                if (model.Amount > remaining) return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
                };

                var payment = new Payment
                {
                    PurchaseInvoiceId = invoice.Id,
                    Amount = model.Amount,
                    PaymentDate = model.PaymentDate,
                    Method = model.PaymentMethod,
                    Description = model.Description,
                    CreatedById = User.GetUserId(),
                    CreatedAt = DateTime.Now
                };

                _context.paymentUW.Create(payment);
                _context.SaveAsync();

                var totalPaid = _context.paymentUW
                    .Get(x => x.PurchaseInvoiceId == invoice.Id && !x.IsDeleted)
                    .Sum(x => x.Amount);

                invoice.PaidAmount = totalPaid;
                invoice.PayStatus = totalPaid <= 0
                    ? SettlementStatus.Unpaid
                    : totalPaid < invoice.Total
                        ? SettlementStatus.Partial
                        : SettlementStatus.Paid;

                _context.purchaseInvoiceUW.Update(invoice);
                _context.SaveAsync();

                return new ApiResponse
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                    AdditionalData = invoice
                
                };
            }
            catch (Exception ex)
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